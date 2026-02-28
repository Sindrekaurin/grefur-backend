using grefurBackend.Events;
using grefurBackend.Events.Queries;
using System;
using System.Threading.Tasks;
using grefurBackend.Infrastructure;

namespace grefurBackend.Api.Rest.V1;

public partial class TriggerController
{
    private async Task<object> HandleRetrieveLogsAsync(string correlationId, Dictionary<string, string> props)
    {
        // Henter deviceId fra JSON-objektet: { "deviceId": "Grefur_3461" }
        props.TryGetValue("deviceId", out var deviceId);

        if (string.IsNullOrEmpty(deviceId))
        {
            return new { Status = "Error", Message = "deviceId missing in props JSON" };
        }

        var tcs = new TaskCompletionSource<RetrieveLogsResponseEvent>();

        var responseHandler = new InlineEventHandler<RetrieveLogsResponseEvent>(async Evt =>
        {
            if (Evt.CorrelationId == correlationId)
            {
                tcs.TrySetResult(Evt);
            }
            await Task.CompletedTask;
        });

        _eventBus.Subscribe(responseHandler);

        try
        {
            // Vi kan også hente 'limit' fra props hvis ønskelig
            int limit = props.TryGetValue("limit", out var l) && int.TryParse(l, out var parsed) ? parsed : 20;

            await _eventBus.Publish(new RetrieveLogsQuery(deviceId, limit, correlationId));

            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == tcs.Task)
            {
                var result = await tcs.Task;
                return new
                {
                    Event = "RetrieveLogs",
                    Status = "Success",
                    Data = result.Data,
                    CorrelationId = correlationId
                };
            }

            return new { Event = "RetrieveLogs", Status = "Timeout", CorrelationId = correlationId };
        }
        finally
        {
            _eventBus.Unsubscribe(responseHandler);
        }
    }
}