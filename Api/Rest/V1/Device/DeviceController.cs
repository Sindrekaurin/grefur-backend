using Microsoft.AspNetCore.Mvc;
using grefurBackend.Models;
using grefurBackend.Services;
using grefurBackend.Events.Device;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using grefurBackend.Helpers;
using grefurBackend.Types.Dto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace grefurBackend.Controllers.Api.Rest.V1.Devices;

[ApiController]
[Route("api/rest/v1/devices")]
[Authorize]
public class DeviceController : ControllerBase
{
    private readonly DeviceService _deviceService;
    private readonly VirtualSensorService _virtualSensorService;
    private readonly MqttService _mqttService;
    private readonly ILogger<DeviceController> _logger;

    /* Summary of function: Constructor for DeviceController initializing core services including the virtual sensor logic */
    public DeviceController(
        DeviceService deviceService,
        VirtualSensorService virtualSensorService,
        MqttService mqttService,
        ILogger<DeviceController> logger)
    {
        _deviceService = deviceService;
        _virtualSensorService = virtualSensorService;
        _mqttService = mqttService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDevices()
    {
        var customerId = User.FindFirst("customerId")?.Value ?? string.Empty;
        var roleStr = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? string.Empty;

        // 1. Vi må parse strengen til UserRole enum for å kunne sammenligne og sende den videre
        if (!Enum.TryParse<UserRole>(roleStr, out var userRole))
        {
            return Unauthorized("Invalid or missing role claim");
        }

        // 2. Nå kan vi sjekke mot enumen (fikser CS0019)
        if (string.IsNullOrEmpty(customerId) && userRole != UserRole.SystemAdmin)
        {
            return Unauthorized("Missing customer configuration.");
        }

        try
        {
            // 3. Vi sender nå 'userRole' (enum), ikke 'roleStr' (string) (fikser CS1503)
            var devices = await _deviceService.GetDevicesForUser(customerId, userRole).ConfigureAwait(false);
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching devices");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
    {
        try
        {
            var result = await _deviceService.RegisterDevice(request).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation("New Grefur device registered: {DeviceId}", request.DeviceId);
                return Ok(new { success = true, deviceId = result.DeviceId });
            }

            if (result.WasConflict)
            {
                return Conflict(new { message = "Device already registered", reason = result.ErrorReason });
            }

            if (result.WasNotFound)
            {
                return NotFound(new { message = "Customer not found", reason = result.ErrorReason });
            }

            return BadRequest(new { message = "Registration failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device registration for {DeviceId}", request.DeviceId);
            return StatusCode(500, "Internal error during registration");
        }
    }

    [HttpPatch("{deviceId}/heartbeat")]
    public async Task<IActionResult> UpdateHeartbeat(string deviceId)
    {
        var success = await _deviceService.UpdateHeartbeat(deviceId).ConfigureAwait(false);
        if (!success) return NotFound();

        return Ok(new { updatedAt = DateTime.UtcNow });
    }

    [HttpGet("{deviceId}")]
    public async Task<IActionResult> GetDeviceData(string deviceId)
    {
        var device = await _deviceService.GetDeviceById(deviceId).ConfigureAwait(false);
        if (device == null) return NotFound();

        return Ok(device);
    }

    [HttpDelete("{deviceId}")]
    public async Task<IActionResult> DeleteDevice(string deviceId, [FromQuery] bool hardDelete = false)
    {
        // Bruk ?? string.Empty for å unngå CS8604 advarselen vi så tidligere
        var customerId = User.FindFirst("customerId")?.Value ?? string.Empty;
        var roleStr = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? string.Empty;

        if (!Enum.TryParse<UserRole>(roleStr, out var userRole)) return Unauthorized();

        try
        {
            // Nå sender vi trygge verdier
            var success = await _deviceService.DeleteDevice(deviceId, customerId, userRole, hardDelete).ConfigureAwait(false);
            if (!success) return Forbid();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device {DeviceId}", deviceId);
            return StatusCode(500, "Internal error");
        }
    }

    [HttpGet("metadata")]
    [AllowAnonymous]
    public IActionResult GetDeviceMetadata()
    {
        return Ok(new
        {
            singleName = "Device",
            verboseName = "Devices",
            description = "Represents a registered IoT device in the Grefur platform"
        });
    }


    /*
     * Endpoint: /api/rest/v1/devices/auth
     * Device have unchangable token from production
     * 1. Device POST token to endpoint
     * 2. Validate token against database records
     * 3. Generate JWT token with limited lifetime for device to use in further communication
     * 4. Token schould contain deviceId and customerId and unchangable token.
     * 4. Return JWT token to device
     * 
     * Endpoint: /api/rest/v1/devices/renew/broker/credentials
     * 1. Device requests new broker credentials
     * 2. Validate device identity by JWT token
     * 3. Verify ids for customer and device against database records
     * 4. Verify device unchangable token against database records
     * 5. Generate new broker credentials
     * 6. Return JWT token to device and add broker credentials to it
     */

    /* Summary of function: Authenticates device via production token and returns both JWT and MQTT credentials */
    [HttpPost("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> AuthenticateDevice([FromBody] DeviceAuthRequest request)
    {

        // Serialiserer objektet tilbake til en JSON-streng
        string requestJson = JsonSerializer.Serialize(request);
        Console.WriteLine("Received authentication request: " + requestJson);

        if (string.IsNullOrEmpty(request.ServiceValidationToken))
        {
            _logger.LogInformation("Authentication attempt with missing production token.");
            return BadRequest("Production token is required.");
        }

        

        try
        {
            // 1. Valider enheten mot databasen via productionToken (Hardware ID)
            var device = await _deviceService.GetDeviceByServiceValidationToken(request.ServiceValidationToken).ConfigureAwait(false);

            if (device == null)
            {
                _logger.LogInformation("Unauthorized access attempt with token: {TokenHash}",
                    request.ServiceValidationToken.Length > 5 ? request.ServiceValidationToken.Substring(0, 5) + "..." : "****");
                return Unauthorized("Invalid production token.");
            }

            _logger.LogInformation("Device {DeviceId} found for authentication.", device.DeviceId);

            // 2. Generer unike MQTT-legitimasjoner for denne sesjonen
            // Vi bruker device.DeviceId som brukernavn i EMQX
            var mqttUser = Guid.NewGuid().ToString("N").Substring(0, 16);
            var mqttPassword = Guid.NewGuid().ToString("N").Substring(0, 16);

            // Oppdater brokeren (EMQX) med de nye detaljene
            await _mqttService.DeleteBrokerUserAsync(mqttUser).ConfigureAwait(false);
            await _mqttService.CreateBrokerUserAsync(mqttUser, mqttPassword).ConfigureAwait(false);

            var brokerConfig = _mqttService.GetBrokerSettings();

            // 3. Generer en enhets-spesifikk JWT for fremtidige API-kall (f.eks OTA)
            var claims = new List<Claim>
        {
            new Claim("deviceId", device.DeviceId),
            new Claim("customerId", device.CustomerId ?? string.Empty),
            new Claim("productionToken", request.ServiceValidationToken),
            new Claim(ClaimTypes.Role, "Device"),
            new Claim("mqttUser", device.DeviceId)
        };

            var token = JwtHelper.GenerateDeviceJwt(claims, expiresHours: 48);

            _logger.LogInformation("Device {DeviceId} authenticated successfully. MQTT user created.", device.DeviceId);

            // 4. Returns all parameters in mqtt which matches ESP32 MqttManager::authenticate()
            return Ok(new
            {
                success = true,
                token = token,
                expiresIn = 172800, // 48 timer (matcher JWT)
                mqtt = new
                {
                    username = mqttUser,
                    password = mqttPassword,
                    brokerHost = brokerConfig.broker,
                    port = brokerConfig.port,
                    // Inkluderer WS URL i tilfelle frontend-debuging eller mobil-app
                    wsUrl = DotNetEnv.Env.GetString("MQTT_BROKER_PUBLIC_URL") ?? $"ws://{brokerConfig.broker}:8083/mqtt"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device authentication for token: {Token}", request.ServiceValidationToken);
            return StatusCode(500, "Internal server error during authentication.");
        }
    }

    /* Summary of function: Internal endpoint to fetch a paginated list of all active virtual devices globally, limited to 10 per request */
    /*[HttpGet("virtual")]
    public async Task<IActionResult> GetVirtualDevices([FromQuery] int page = 1)
    {
        try
        {
            // Enforce a hard limit of 10 devices per request for staff overview
            const int pageSize = 10;

            /* Summary of function: Calling service to get all enabled virtual devices globally */
            /*var devices = await _virtualSensorService.GetAllActiveVirtualDevicesAsync(page, pageSize).ConfigureAwait(false);

            return Ok(new
            {
                success = true,
                data = devices,
                metadata = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    count = devices.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Grefur Admin]: Error retrieving global virtual devices");
            return StatusCode(500, "Internal server error");
        }
    }*/

    /* Summary of function: Endpoint for creating a new virtual sensor device for the authenticated customer */
    [HttpPost("virtual")]
    public async Task<IActionResult> CreateVirtualDevice([FromBody] VirtualDeviceRegistrationRequest request)
    {
        try
        {
            var result = await _virtualSensorService.CreateVirtualDevice(request).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Virtual device {DeviceId} created for customer {CustomerId}", request.DeviceId);
                return Ok(new { success = true, deviceId = request.DeviceId });
            }

            return BadRequest(new { message = result.ErrorReason ?? "Failed to create virtual device" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating virtual device {DeviceId}", request.DeviceId);
            return StatusCode(500, "Internal server error");
        }
    }



}