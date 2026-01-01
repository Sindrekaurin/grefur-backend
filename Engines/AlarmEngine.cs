using System;
using System.Threading.Tasks;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Queries;
using grefurBackend.Events;
using grefurBackend.Models;

namespace grefurBackend.Engines;

public class AlarmEngine :
    IEventHandler<ValueReceivedEvent>,
    IEventHandler<ResponseCustomerValueEnrichmentEvent>
{
    private readonly EventBus _eventBus;
    private readonly AlarmService _alarmService;
    private readonly ILogger<AlarmEngine> _logger;

    public AlarmEngine(EventBus eventBus, AlarmService alarmService, ILogger<AlarmEngine> logger)
    {
        _eventBus = eventBus;
        _alarmService = alarmService;
        _logger = logger;

        _eventBus.Subscribe<ValueReceivedEvent>(this);
        _eventBus.Subscribe<ResponseCustomerValueEnrichmentEvent>(this);
    }

    public async Task Handle(ResponseCustomerValueEnrichmentEvent almEvt)
    {
        // Check subscription level using the Enum from the Customer object
        if (almEvt.AlarmPolicyLevel == AlarmLevel.None)
        {
            _logger.LogInformation("[AlarmEngine]: Alarm analysis denied for customer {CustomerId}", almEvt.Customer.CustomerId);
            return;
        }

        _logger.LogInformation("[AlarmEngine]: Enrichment received for Customer {CustomerId}", almEvt.Customer.CustomerId);

        // Logic for ML analysis or threshold checking would go here
        if (double.TryParse(almEvt.Value, out double numericValue))
        {
            if (_alarmService.AnalyzeValue(almEvt.Topic, numericValue, out var message))
            {
                _logger.LogWarning("[AlarmEngine]: Alarm triggered for topic {Topic}: {Message}", almEvt.Topic, message);

                var alarmEvent = new AlarmRaisedEvent(
                    Source: nameof(AlarmEngine),
                    CorrelationId: almEvt.CorrelationId,
                    CustomerId: almEvt.Customer.CustomerId,
                    DeviceId: almEvt.DeviceId,
                    Topic: almEvt.Topic,
                    Value: numericValue,
                    Message: message
                );

                await _eventBus.Publish(alarmEvent).ConfigureAwait(false);
            }
        }
    }

    public async Task Handle(ValueReceivedEvent evt)
    {
        // Raw values are handled by SubscriptionEngine to trigger enrichment.
        // AlarmEngine waits for the enriched response to make decisions.
        await Task.CompletedTask;
    }
}