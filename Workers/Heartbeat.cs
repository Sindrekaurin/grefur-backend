using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using grefurBackend.Events;
using grefurBackend.Events.Lifecycle;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using grefurBackend.Types;

namespace grefurBackend.Workers;

/* Summary of class: specialized worker responsible for system health signaling. 
   Triggered every 15s via ILevel3Task orchestration. */
public class HeartbeatWorker : ILevel3Task
{
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly EventBus _eventBus;
    private readonly CustomerUsageCoordinator _usageCoordinator;

    public HeartbeatWorker(
        ILogger<HeartbeatWorker> logger,
        EventBus eventBus,
        CustomerUsageCoordinator usageCoordinator)
    {
        _logger = logger;
        _eventBus = eventBus;
        _usageCoordinator = usageCoordinator;
    }

    /* Summary of function: Executes the heartbeat publication logic including buffer metrics. */
    public async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            var heartBeat = new SystemHeartBeat(
                source: nameof(HeartbeatWorker),
                correlationId: Guid.NewGuid().ToString(),
                payload: new
                {
                    timestamp = DateTime.UtcNow,
                    status = "Healthy",
                    bufferSize = _usageCoordinator.GetBufferSize()
                }
            );

            await _eventBus.Publish(heartBeat);
            _logger.LogInformation("[HeartbeatWorker]: System heartbeat published.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeartbeatWorker]: Failed to publish heartbeat.");
        }
    }
}