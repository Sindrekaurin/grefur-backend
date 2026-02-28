using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using grefurBackend.Events.Domain;
using grefurBackend.Events;
using grefurBackend.Events.Queries;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using grefurBackend.Models.AlarmConfiguration;
using Microsoft.Extensions.Logging;
using grefurBackend.Models;

namespace grefurBackend.Engines;

public class PredictionEngine : IEventHandler<TrainAndPublishEvent>, IEventHandler<ResponseCustomerValueEnrichmentEvent>
{
    private readonly EventBus _eventBus;
    private readonly MlTrainingService _mlTrainingService;
    private readonly AlarmService _alarmService;
    private readonly ILogger<PredictionEngine> _logger;

    public PredictionEngine(
        EventBus eventBus,
        MlTrainingService mlTrainingService,
        AlarmService alarmService,
        ILogger<PredictionEngine> logger)
    {
        _eventBus = eventBus;
        _mlTrainingService = mlTrainingService;
        _alarmService = alarmService;
        _logger = logger;

        _eventBus.Subscribe<TrainAndPublishEvent>(this);
        _eventBus.Subscribe<ResponseCustomerValueEnrichmentEvent>(this);
    }

    public async Task Handle(ResponseCustomerValueEnrichmentEvent enrichEvt)
    {
        try
        {
            if (enrichEvt.Customer.AlarmSubscription != AlarmLevel.Premium)
            {
                _logger.LogDebug("[{EngineName}]: ML prediction skipped. Customer {CustomerId} is not on Premium Alarm level.",
                    nameof(PredictionEngine), enrichEvt.Customer.CustomerId);
                return;
            }

            // Functionality not implemented yet
            /*var mlConfigs = await _alarmService.GetMlConfigurationsForCustomerAsync(enrichEvt.CustomerId);
            var targetConfig = mlConfigs.FirstOrDefault(c => c.TargetMeasurementId == enrichEvt.Topic && c.IsEnabled);*/

            _logger.LogInformation("[{EngineName}]: Running ML inference for {Topic} (Customer: {CustomerId})",
            nameof(PredictionEngine), enrichEvt.Topic, enrichEvt.Customer.CustomerId);

            // Get features
            //var currentFeatures = await GetCurrentFeatureValuesAsync(targetConfig);

            await Task.Yield(); // Fixes CS1998: yields control to avoid synchronous execution warning
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{EngineName}]: Error during real-time prediction handle", nameof(PredictionEngine));
        }
    }

    public async Task Handle(TrainAndPublishEvent trainEvt)
    {
        try
        {
            _logger.LogInformation(
                "[{EngineName}]: Received TrainAndPublishEvent for Customer: {CustomerId}, Target: {TargetId}",
                nameof(PredictionEngine),
                trainEvt.Configuration.CustomerId,
                trainEvt.Configuration.TargetMeasurementId
            );

            var result = await _mlTrainingService.TrainAndPublish(trainEvt.Configuration).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[{EngineName}]: Training successful for {TargetId}.",
                    nameof(PredictionEngine),
                    trainEvt.Configuration.TargetMeasurementId
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{EngineName}]: Failed to handle TrainAndPublishEvent", nameof(PredictionEngine));
            throw;
        }
    }

    private async Task<float[]?> GetCurrentFeatureValuesAsync(MlAlarmConfiguration config)
    {
        // This will fetch the latest values from the cache or database
        // for all topics defined in config.FeatureMeasurementIds
        await Task.Yield();
        return null; // Temporary placeholder - Fixes CS8603 by allowing null return
    }
}