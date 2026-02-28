using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using grefurBackend.Services;
using grefurBackend.Types;
using grefurBackend.Models.AlarmConfiguration;

namespace grefurBackend.Workers;

/* Summary of class: Weekly worker that identifies and triggers ML training for eligible alarm configurations. */
public class MlTrainingWorker : IPlannedTask
{
    private readonly ILogger<MlTrainingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // Planlagt for hver søndag kl. 02:00
    public int ExecutionHour => 2;
    public int ExecutionMinute => 0;
    public DateTime LastRunDate { get; set; } = DateTime.MinValue;
    public DayOfWeek? ExecutionDayOfWeek => DayOfWeek.Sunday;
    public int? ExecutionDayOfMonth => null;
    public bool? RunOnLastDayOfMonth => null;

    public bool ForceRunNow { get; set; } = false;

    public MlTrainingWorker(
        ILogger<MlTrainingWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /* Summary of function: Finds all active weekly ML configurations and triggers the training service. */
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[MlTrainingWorker]: Starting weekly ML training cycle...");

        using var scope = _scopeFactory.CreateScope();
        var trainingService = scope.ServiceProvider.GetRequiredService<MlTrainingService>();
        var alarmService = scope.ServiceProvider.GetRequiredService<AlarmService>();

        try
        {
            // 1. Hent alle konfigurasjoner som er satt til Weekly
            var allConfigs = await alarmService.GetAllMlAlarmConfigurationsAsync(ct);
            var weeklyConfigs = allConfigs
                .Where(c => c.IsEnabled && c.TrainingFrequency == TrainingFrequency.Weekly)
                .ToList();

            _logger.LogInformation("[MlTrainingWorker]: Found {Count} weekly configurations to train.", weeklyConfigs.Count);

            foreach (var config in weeklyConfigs)
            {
                if (ct.IsCancellationRequested) break;

                _logger.LogInformation("[MlTrainingWorker]: Triggering training for {AlarmId}", config.MlAlarmId);
                var result = await trainingService.TrainAndPublish(config, ct);

                if (result.Success)
                {
                    _logger.LogInformation("[MlTrainingWorker]: Successfully trained {AlarmId}.", config.MlAlarmId);
                }
                else
                {
                    _logger.LogWarning("[MlTrainingWorker]: Training failed for {AlarmId}: {Message}", config.MlAlarmId, result.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MlTrainingWorker]: Critical error during weekly training cycle.");
        }

        ForceRunNow = false;
    }
}