using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using grefurBackend.Models;
using grefurBackend.Context;
using grefurBackend.Infrastructure;
using grefurBackend.Types.Dto;

namespace grefurBackend.Services
{
    /* Summary of function: Service to manage virtual sensors, fetching remote API data and publishing to the EMQX broker using grefur's specific topic structure */
    public class VirtualSensorService
    {
        private readonly ILogger<VirtualSensorService> _logger;
        private readonly IDbContextFactory<MySqlContext> _mySqlContextFactory;
        private readonly UserService _userService;
        private readonly MqttService _mqttService;
        private readonly HttpClient _httpClient;

        public VirtualSensorService(
            ILogger<VirtualSensorService> Logger,
            IDbContextFactory<MySqlContext> MySqlContextFactory,
            UserService UserService,
            MqttService MqttService)
        {
            _logger = Logger;
            _mySqlContextFactory = MySqlContextFactory;
            _userService = UserService;
            _mqttService = MqttService;

            /* Summary of function: Initializing a reusable HttpClient for the lifetime of the service */
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "grefur-sensor-v1");
        }

        /* Summary of function: Logic for mapping DTO to VirtualGrefurDevice entity and persisting it to MySQL */
        public async Task<DeviceRegistrationResult> CreateVirtualDevice(VirtualDeviceRegistrationRequest request)
        {
            using var context = await _mySqlContextFactory.CreateDbContextAsync();

            var exists = await context.VirtualGrefurDevice.AnyAsync(d => d.DeviceId == request.DeviceId);
            if (exists) return DeviceRegistrationResult.Failure("Device ID already exists.");

            /* Summary of function: Initializing a new virtual device as a global grefur template */

            //
            var virtualDevice = new VirtualGrefurDevice
            {
                DeviceId = request.DeviceId,

                DeviceName = request.FrontendHeader ?? request.DeviceId,
                DeviceType = "VirtualSensor",
                SoftwareVersion = "1.1.0",
                HardwareVersion = "Cloud-Logic-v2",
                MetadataJson = "{}",

                ApiProviderUrl = request.ApiProviderUrl,
                RequestHeadersJson = request.RequestHeadersJson,
                RequestBodyJson = request.RequestBodyJson,

                FrontendHeader = request.FrontendHeader,
                IsEnabled = true,
                LastSignOfLife = DateTime.UtcNow,

                // Mapping the advanced sensor values including scaling and types
                SensorValues = request.SensorMappings.Select(v => new VirtualSensorValue
                {
                    KeyName = v.MqttKey,
                    JsonPath = v.JsonPath,
                    Unit = v.Unit,
                    DataType = v.DataType,
                    Multiplier = v.Multiplier,
                    DeviceId = request.DeviceId
                }).ToList()
            };

            // Trigger the initial fetch schedule
            virtualDevice.ScheduleNextFetch();

            try
            {
                context.VirtualGrefurDevice.Add(virtualDevice);
                await context.SaveChangesAsync();
                _logger.LogInformation("[Virtual Service]: Successfully provisioned device template {DeviceId}", request.DeviceId);
                return DeviceRegistrationResult.Success(virtualDevice.DeviceId);

            } catch (Exception ex)
            {
                _logger.LogError("[Virtual Service]: Error while saving new virtual device");
                return DeviceRegistrationResult.Failure(virtualDevice.DeviceId);
            }




            
        }


        /* Summary of function: Retrieves a global list of the first 10 active virtual devices, ignoring customer boundaries for admin oversight */
        public async Task<List<VirtualGrefurDevice>> GetAllActiveVirtualDevicesAsync(int page, int pageSize)
        {
            using var context = await _mySqlContextFactory.CreateDbContextAsync();

            /* Summary of function: Queries all devices where IsEnabled is true, using Skip and Take for the requested page size of 10 */
            return await context.Set<VirtualGrefurDevice>()
                .Include(d => d.SensorValues)
                .Where(d => d.IsEnabled)
                .OrderByDescending(d => d.LastSignOfLife)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}