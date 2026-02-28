using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using grefurBackend.Context;
using grefurBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class DeviceDiscoveryEngine : IEventHandler<CustomerLoadedEvent>
{
    private readonly EventBus _eventBus;
    private readonly CacheService _cacheService;
    private readonly MqttService _mqttService;
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly ILogger<DeviceDiscoveryEngine> _logger;

    public DeviceDiscoveryEngine(
        EventBus eventBus,
        CacheService cacheService,
        MqttService mqttService,
        IDbContextFactory<MySqlContext> contextFactory,
        ILogger<DeviceDiscoveryEngine> logger)
    {
        _eventBus = eventBus;
        _cacheService = cacheService;
        _mqttService = mqttService;
        _contextFactory = contextFactory;
        _logger = logger;

        _eventBus.Subscribe<CustomerLoadedEvent>(this);
    }

    public async Task Handle(CustomerLoadedEvent evt)
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var devices = await context.GrefurDevices
            .Where(d => d.CustomerId == evt.CustomerId && !d.IsDeletedByCustomer)
            .ToListAsync()
            .ConfigureAwait(false);

        if (!devices.Any())
        {
            _logger.LogInformation("[DeviceDiscoveryEngine]: No active devices found for customer {CustomerId}", evt.CustomerId);
            return;
        }

        foreach (var device in devices)
        {
            var deviceRegisteredEvent = new DeviceRegisteredEvent(
                customerId: device.CustomerId,
                deviceId: device.DeviceId,
                source: nameof(DeviceDiscoveryEngine),
                correlationId: evt.CorrelationId
            );

            await _eventBus.Publish(deviceRegisteredEvent).ConfigureAwait(false);
        }

        _logger.LogInformation("[DeviceDiscoveryEngine]: Published registration events for {Count} devices owned by {CustomerId}", devices.Count, evt.CustomerId);
    }
}