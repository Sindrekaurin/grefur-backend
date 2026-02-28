namespace grefurBackend.Workers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using grefurBackend.Services;
using grefurBackend.Types;

/* Summary of class: Nightly maintenance worker that runs once every night at 03:00 UTC. */
public class LogSmoothingWorker : IPlannedTask
{
    private readonly ILogger<LogSmoothingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // IPlannedTask Properties
    public int ExecutionHour => 3;
    public int ExecutionMinute => 0;
    public DateTime LastRunDate { get; set; } = DateTime.MinValue;

    // Optional constraints (all set to null as this is a simple nightly task)
    public DayOfWeek? ExecutionDayOfWeek => null;
    public int? ExecutionDayOfMonth => null;
    public bool? RunOnLastDayOfMonth => null;

    public LogSmoothingWorker(
        ILogger<LogSmoothingWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /* Summary of function: Executes the maintenance logic via a scoped LoggerService. */
    public async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[LogSmoothingWorker]: Running planned maintenance...");

            using var scope = _scopeFactory.CreateScope();
            var loggerService = scope.ServiceProvider.GetRequiredService<LoggerService>();

            await loggerService.RunMaintenanceSmoothingAsync(ct);

            _logger.LogInformation("[LogSmoothingWorker]: Maintenance completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LogSmoothingWorker]: Failed to execute planned smoothing.");
            // Vi kaster ikke exception her for å unngå at hele motoren stopper, 
            // men LastRunDate blir ikke oppdatert i ScheduleService hvis denne feiler (valgfritt).
        }
    }
}