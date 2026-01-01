using System.Threading.Tasks;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Queries;
using grefurBackend.Events;

namespace grefurBackend.Engines;

public class LoggerEngine : IEventHandler<ResponseCustomerValueEnrichmentEvent>
{
    private readonly EventBus _eventBus;
    private readonly LoggerService _loggerService;
    private readonly ILogger<LoggerEngine> _logger;

    public LoggerEngine(EventBus eventBus, LoggerService loggerService, ILogger<LoggerEngine> logger)
    {
        _eventBus = eventBus;
        _loggerService = loggerService;
        _logger = logger;

        _eventBus.Subscribe<ResponseCustomerValueEnrichmentEvent>(this);
    }

    public async Task Handle(ResponseCustomerValueEnrichmentEvent evt)
    {
        // Bruker PascalCase: LogPolicyLevel og CustomerId
        if (evt.LogPolicyLevel <= 0)
        {
            _logger.LogInformation("[LoggerEngine]: Logging denied for customer {CustomerId}", evt.Customer.CustomerId);
            return;
        }

        // Oppretter LogPointEvent med PascalCase properties fra evt
        var logPointEvent = new LogPointEvent(
            customerId: evt.Customer.CustomerId,
            deviceId: evt.DeviceId,
            topic: evt.Topic,
            valueType: evt.ValueType,
            value: evt.Value,
            status: LogPointStatus.Requested,
            source: nameof(LoggerEngine),
            correlationId: evt.CorrelationId
        );

        await _eventBus.Publish(logPointEvent).ConfigureAwait(false);

        _logger.LogDebug(
            "[LoggerEngine]: LogPointEvent published for topic {Topic} = {Value}",
            evt.Topic,
            evt.Value
        );
    }
}