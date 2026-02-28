using System;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using grefurBackend.Context;
using grefurBackend.Models;
using grefurBackend.Events.Device;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

/*
 * Engine activated when new devices are registered to handle authentication and authorization.
 * * Subscribe to DeviceAuthEvent to process device authentication requests.
 * 1. Validate event with database records.
 * 2. Use MQTT service to reqeuest login credentials to broker for the device.
 * 3. POST credentials to device management API.
 * 4. If not successful, send DeviceAuthEvent with AuthStatus AuthFailed with same correlaionId. This will activate a new function for scheduling retries.
 * 5. If successful, send DeviceAuthEvent with AuthStatus Authenticated with same correlaionId.
 * * * */
public class DeviceAuthEngine : IEventHandler<DeviceAuthEvent>
{
    private readonly EventBus _eventBus;
    private readonly MqttService _mqttService;
    private readonly DeviceService _deviceService;
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly ILogger<DeviceAuthEngine> _logger;

    public DeviceAuthEngine(
        EventBus eventBus,
        MqttService mqttService,
        DeviceService deviceService,
        IDbContextFactory<MySqlContext> contextFactory,
        ILogger<DeviceAuthEngine> logger)
    {
        _eventBus = eventBus;
        _mqttService = mqttService;
        _deviceService = deviceService;
        _contextFactory = contextFactory;
        _logger = logger;

        _eventBus.Subscribe<DeviceAuthEvent>(this);
    }

    public async Task Handle(DeviceAuthEvent evt)
    {
        if (evt.AuthStatus != DeviceAuthStatus.NeedAuth)
        {
            return;
        }

        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var device = await context.GrefurDevices
            .FirstOrDefaultAsync(d => d.DeviceId == evt.DeviceId && d.CustomerId == evt.CustomerId)
            .ConfigureAwait(false);

        if (device == null)
        {
            _logger.LogWarning("[DeviceAuthEngine]: Device {DeviceId} validation failed during auth process", evt.DeviceId);
            return;
        }

        var success = false;
        try
        {
            var username = $"{device.DeviceId}";
            var password = Guid.NewGuid().ToString("N");

            var credsCreatedSucessfully = await _mqttService.CreateBrokerUserAsync(username, password).ConfigureAwait(false);

            if (credsCreatedSucessfully)
            {
                // Logic for posting to device should be implemented in DeviceService or a Management service
                // success = await _deviceService.PostCredentialsToDevice(device.IpAddress, username, password).ConfigureAwait(false);

                // For now, assuming broker creation is the success criteria if the device POST is not yet implemented
                success = true;
            }

            if (success)
            {
                await PublishAuthStatus(evt, DeviceAuthStatus.Authenticated).ConfigureAwait(false);
            }
            else
            {
                await PublishAuthStatus(evt, DeviceAuthStatus.AuthFailed).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DeviceAuthEngine]: Error processing auth for device {DeviceId}", device.DeviceId);
            await PublishAuthStatus(evt, DeviceAuthStatus.AuthFailed).ConfigureAwait(false);
        }
    }

    private async Task PublishAuthStatus(DeviceAuthEvent rootEvt, DeviceAuthStatus authStatus)
    {
        var resultEvent = new DeviceAuthEvent(
            source: nameof(DeviceAuthEngine),
            correlationId: rootEvt.CorrelationId,
            customerId: rootEvt.CustomerId,
            deviceId: rootEvt.DeviceId,
            authStatus: authStatus
        );

        await _eventBus.Publish(resultEvent).ConfigureAwait(false);
    }
}