using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Events.Lifecycle;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class DeviceDiscoveryEngine : IEventHandler<CustomerLoadedEvent>
{
    private readonly EventBus _eventBus;
    private readonly CacheService _cacheService;
    private readonly MqttService _mqttService;
    private readonly ILogger<DeviceDiscoveryEngine> _logger;

    public DeviceDiscoveryEngine(EventBus eventBus, CacheService cacheService, MqttService mqttService, ILogger<DeviceDiscoveryEngine> logger)
    {
        _eventBus = eventBus;
        _cacheService = cacheService;
        _mqttService = mqttService;
        _logger = logger;

        _eventBus.Subscribe<CustomerLoadedEvent>(this);
    }

    public async Task Handle(CustomerLoadedEvent evt)
    {
        // Nå vil evt.CustomerId fungere fordi vi la det til i CustomerLoadedEvent
        _logger.LogInformation("[DeviceDiscoveryEngine]: CustomerLoadedEvent received: {CustomerId}", evt.CustomerId);

        if (evt.Customer?.RegisteredDevices == null)
        {
            _logger.LogWarning("[DeviceDiscoveryEngine]: No devices found for customer {CustomerId}", evt.CustomerId);
            return;
        }

        foreach (var deviceId in evt.Customer.RegisteredDevices)
        {
            _logger.LogInformation("[DeviceDiscoveryEngine]: Device discovered: {DeviceId} for customer {CustomerId}", deviceId, evt.CustomerId);

            var deviceRegisteredEvent = new DeviceRegisteredEvent(
                customerId: evt.CustomerId,
                deviceId: deviceId,
                source: nameof(DeviceDiscoveryEngine),
                correlationId: evt.CorrelationId
            );

            await _eventBus.Publish(deviceRegisteredEvent).ConfigureAwait(false);
        }
    }
}