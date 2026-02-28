using System;
using System.Threading.Tasks;
using grefurBackend.Events.Domain;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;
using grefurBackend.Events.Queries;
using grefurBackend.Events;

namespace grefurBackend.Engines;

public class LoggerEngine :
    IEventHandler<ResponseCustomerValueEnrichmentEvent>,
    IEventHandler<RetrieveLogsQuery>
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
        _eventBus.Subscribe<RetrieveLogsQuery>(this);
    }

    public async Task Handle(ResponseCustomerValueEnrichmentEvent evt)
    {
        if (evt.LogPolicyLevel <= 0)
        {
            _logger.LogDebug("[LoggerEngine]: Logging denied for customer {CustomerId}", evt.Customer.CustomerId);
            return;
        }

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

    public async Task Handle(RetrieveLogsQuery query)
    {
        _logger.LogInformation("[LoggerEngine]: Query received for DeviceId {DeviceId}", query.DeviceId);

        try
        {
            // Bruker servicen direkte for å hente data fra TimescaleDB
            var logs = await _loggerService.getLatestLogsAsync(query.DeviceId, query.Limit);

            Console.WriteLine($"[LoggerEngine]: Retrieved {logs.Count} logs for DeviceId {query.DeviceId}");

            // Mapper resultatet til en Response-event med samme CorrelationId
            var responseEvent = new RetrieveLogsResponseEvent(
                success: true,
                correlationId: query.CorrelationId,
                data: logs,
                message: $"Successfully retrieved {logs.Count} entries for {query.DeviceId}"
            );

            await _eventBus.Publish(responseEvent).ConfigureAwait(false);

            _logger.LogInformation("[LoggerEngine]: RetrieveLogsResponseEvent published for CorrelationId {Id}", query.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LoggerEngine]: Error processing RetrieveLogsQuery for {DeviceId}", query.DeviceId);

            var errorResponse = new RetrieveLogsResponseEvent(
                success: false,
                correlationId: query.CorrelationId,
                message: $"Failed to retrieve logs: {ex.Message}"
            );

            await _eventBus.Publish(errorResponse).ConfigureAwait(false);
        }
    }

    

}