using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Context;
using grefurBackend.Models;
using grefurBackend.Events.Device;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Services;

/*
 * Engine activated when new devices are registered to handle authentication and authorization.
 * * Subscribe to DeviceAuthEvent to process device authentication requests.
 * 1. Validate event with database records.
 * 2. Use MQTT service to reqeuest login credentials to broker for the device.
 * 3. POST credentials to device management API.
 * 4. If not successful, send DeviceAuthEvent with AuthStatus AuthFailed with same correlaionId. This will activate a new function for scheduling retries.
 * 5. If successful, send DeviceAuthEvent with AuthStatus Authenticated with same correlaionId.
 * * * */
public class DeviceService
{
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly ILogger<DeviceService> _logger;
    private readonly EventBus _eventBus;

    public DeviceService(
        IDbContextFactory<MySqlContext> contextFactory,
        ILogger<DeviceService> logger,
        EventBus eventBus)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _eventBus = eventBus;
    }

    public async Task<List<GrefurDevice>> GetDevicesForUser(string customerId, UserRole role)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        IQueryable<GrefurDevice> query = context.GrefurDevices;

        if (role != UserRole.SystemAdmin)
        {
            query = query.Where(d => d.CustomerId == customerId && !d.IsDeletedByCustomer);
        }

        return await query
            .OrderByDescending(d => d.LastSignOfLife)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<GrefurDevice?> GetDeviceById(string deviceId)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        return await context.GrefurDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId)
            .ConfigureAwait(false);
    }

    public async Task<GrefurDevice?> GetDeviceByServiceValidationToken(string serviceValidationToken)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        return await context.GrefurDevices
            .FirstOrDefaultAsync(d => d.ServiceValidationToken == serviceValidationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeviceOperationResult> RegisterDevice(DeviceRegistrationRequest request)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var existingDevice = await context.GrefurDevices
            .FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId)
            .ConfigureAwait(false);

        if (existingDevice != null)
        {
            if (existingDevice.IsDeletedByCustomer)
            {
                return await ReactivateDevice(context, existingDevice, request).ConfigureAwait(false);
            }
            return DeviceOperationResult.Conflict("DUPLICATE_DEVICE_ID");
        }

        var customerExists = await context.GrefurCustomers
            .AnyAsync(c => c.CustomerId == request.CustomerId)
            .ConfigureAwait(false);

        if (!customerExists)
        {
            return DeviceOperationResult.NotFound("INVALID_CUSTOMER_ID");
        }

        var newDevice = new GrefurDevice
        {
            DeviceId = request.DeviceId,
            CustomerId = request.CustomerId,
            DeviceName = request.DeviceName ?? request.DeviceId,
            DeviceType = request.DeviceType,
            SoftwareVersion = request.SoftwareVersion,
            HardwareVersion = request.HardwareVersion,
            IsNested = request.IsNested,
            LastSignOfLife = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true,
            HeartbeatIntervalSeconds = 300,
            MetadataJson = "{}",
            ServiceValidationToken = request.ServiceValidationToken,
        };

        context.GrefurDevices.Add(newDevice);
        await context.SaveChangesAsync().ConfigureAwait(false);

        await PublishRegistrationEvents(newDevice.CustomerId, newDevice.DeviceId).ConfigureAwait(false);

        return DeviceOperationResult.Success(newDevice.DeviceId);
    }

    /* Summary of function: Validates that a device exists and is authorized */
    public async Task<bool> VerifyDeviceActive(string deviceId, string customerId, string token)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GrefurDevices.AnyAsync(d =>
            d.DeviceId == deviceId &&
            d.CustomerId == customerId &&
            d.ServiceValidationToken == token &&
            d.IsEnabled);
    }

    public async Task<bool> DeactivateDevice(string deviceId, string customerId)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        var device = await context.GrefurDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.CustomerId == customerId)
            .ConfigureAwait(false);

        if (device == null) return false;

        device.IsEnabled = false;
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<bool> UpdateHeartbeat(string deviceId)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        var device = await context.GrefurDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId)
            .ConfigureAwait(false);

        if (device == null) return false;

        device.LastSignOfLife = DateTime.UtcNow;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteDevice(string deviceId, string customerId, UserRole role, bool hardDelete)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        var device = await context.GrefurDevices
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId)
            .ConfigureAwait(false);

        if (device == null) return false;

        bool isSystemAdmin = role == UserRole.SystemAdmin;
        bool isCustomerAdmin = role == UserRole.Admin && device.CustomerId == customerId;

        if (!isSystemAdmin && !isCustomerAdmin) return false;

        if (hardDelete && isSystemAdmin)
        {
            context.GrefurDevices.Remove(device);
        }
        else
        {
            device.IsDeletedByCustomer = true;
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        await _eventBus.Publish(new DeviceDeletedEvent(
            Source: "grefur-backend-service",
            CorrelationId: Guid.NewGuid().ToString(),
            CustomerId: device.CustomerId,
            DeviceId: device.DeviceId
        )).ConfigureAwait(false);

        return true;
    }

    private async Task<DeviceOperationResult> ReactivateDevice(MySqlContext context, GrefurDevice device, DeviceRegistrationRequest request)
    {
        device.IsDeletedByCustomer = false;
        device.CustomerId = request.CustomerId;
        device.DeviceName = request.DeviceName ?? request.DeviceId;
        device.DeviceType = request.DeviceType;
        device.SoftwareVersion = request.SoftwareVersion;
        device.HardwareVersion = request.HardwareVersion;
        device.IsNested = request.IsNested;
        device.IsEnabled = true;
        device.LastSignOfLife = DateTime.UtcNow;

        await context.SaveChangesAsync().ConfigureAwait(false);
        return DeviceOperationResult.Success(device.DeviceId);
    }

    private async Task PublishRegistrationEvents(string customerId, string deviceId)
    {
        var correlationId = Guid.NewGuid().ToString();

        await _eventBus.Publish(new DeviceRegisteredEvent(
            customerId: customerId,
            deviceId: deviceId,
            source: "grefur-backend-service",
            correlationId: correlationId
        )).ConfigureAwait(false);

        await _eventBus.Publish(new DeviceAuthEvent(
            source: "grefur-backend-service",
            correlationId: correlationId,
            customerId: customerId,
            deviceId: deviceId,
            authStatus: DeviceAuthStatus.NeedAuth
        )).ConfigureAwait(false);
    }
}