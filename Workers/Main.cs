using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using grefurBackend.Services;

namespace grefurBackend.Workers;

/* Summary of class: The heartbeat of the system. 
   Periodically triggers the ScheduleService to process all tiered tasks. */
public class EngineWorker : BackgroundService
{
    private readonly ILogger<EngineWorker> _logger;
    private readonly ScheduleService _scheduleService;

    public EngineWorker(ILogger<EngineWorker> logger, ScheduleService scheduleService)
    {
        _logger = logger;
        _scheduleService = scheduleService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Engine is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _scheduleService.RunPendingTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in EngineWorker loop.");
            }

            await Task.Delay(100, stoppingToken);
        }
    }
}