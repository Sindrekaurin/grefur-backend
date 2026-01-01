using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using grefurBackend.Events.Integration;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Queries;

namespace grefurBackend.Engines;

public class ValueHandlerEngine :
    IEventHandler<MqttMessageReceivedEvent>
{
    private readonly EventBus _eventBus;
    private readonly CacheService _cacheService;
    private readonly ILogger<ValueHandlerEngine> _logger;

    public ValueHandlerEngine(
        EventBus eventBus,
        CacheService cacheService,
        ILogger<ValueHandlerEngine> logger)
    {
        _eventBus = eventBus;
        _cacheService = cacheService;
        _logger = logger;

        _eventBus.Subscribe<MqttMessageReceivedEvent>(this);
    }

    public async Task<string?> queryCustomerIdAsync(string deviceId)
    {
        var queryEvent = new CustomerQueryEvent(
            deviceId: deviceId,
            source: nameof(ValueHandlerEngine),
            correlationId: Guid.NewGuid().ToString()
        );

        var response = await _eventBus.RequestAsync<CustomerQueryEvent, CustomerQueryResponseEvent>(
            queryEvent,
            r => r.DeviceId == deviceId
            //timeoutMs: 5000
        );

        return response?.CustomerId;
    }

    public async Task Handle(MqttMessageReceivedEvent mqttEvt)
    {
        // Denne metoden er nå klar for implementasjon
        await Task.CompletedTask;
    }
}