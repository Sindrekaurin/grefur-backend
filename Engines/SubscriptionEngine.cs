using System;
using System.Threading.Tasks;
using grefurBackend.Events;
using grefurBackend.Events.Queries;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class SubscriptionEngine : IEventHandler<ValueReceivedEvent>
{
    private readonly EventBus _eventBus;
    private readonly LoggerService _loggerService;
    private readonly ILogger<SubscriptionEngine> _logger;

    public SubscriptionEngine(EventBus eventBus, LoggerService loggerService, ILogger<SubscriptionEngine> logger)
    {
        _eventBus = eventBus;
        _loggerService = loggerService;
        _logger = logger;

        _eventBus.Subscribe<ValueReceivedEvent>(this);
    }

    public async Task Handle(ValueReceivedEvent evt)
    {
        try
        {
            _logger.LogInformation("ValueReceivedEvent triggered for device {DeviceId}", evt.DeviceId);

            // Using PascalCase for named arguments to match the class definition
            var customerSubLvlQuery = new RequestCustomerValueEnrichmentEvent(
                ///CustomerId: evt.CustomerId ?? "unknown",
                Source: nameof(SubscriptionEngine),
                CorrelationId: evt.CorrelationId,
                Topic: evt.Topic,
                Value: evt.Value,
                ValueType: evt.ValueType,
                DeviceId: evt.DeviceId
            );

            await _eventBus.Publish(customerSubLvlQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[SubscriptionEngine]: Failed to handle ValueReceivedEvent for topic {Topic}",
                evt.Topic
            );
            throw;
        }
    }
}