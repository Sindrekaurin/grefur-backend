using System;
using System.Threading.Tasks;
using grefurBackend.Events.Domain;
using grefurBackend.Events;
using grefurBackend.Infrastructure;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;

namespace grefurBackend.Engines;

public class PredictionEngine : IEventHandler<TrainAndPublishEvent>
{
    private readonly EventBus _eventBus;
    private readonly MlTrainingService _mlTrainingService;
    private readonly ILogger<PredictionEngine> _logger;

    public PredictionEngine(
        EventBus eventBus,
        MlTrainingService mlTrainingService,
        ILogger<PredictionEngine> logger)
    {
        _eventBus = eventBus;
        _mlTrainingService = mlTrainingService;
        _logger = logger;

        _eventBus.Subscribe<TrainAndPublishEvent>(this);
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

            // Her kaller vi tjenesten som ligger under Engine-nivået direkte
            var result = await _mlTrainingService.TrainAndPublish(trainEvt.Configuration);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[{EngineName}]: Training successful for {TargetId}. Publishing results...",
                    nameof(PredictionEngine),
                    trainEvt.Configuration.TargetMeasurementId
                );

                // Eksempel på å sende et integrasjonsevent videre etter suksessfull trening
                // await _eventBus.Publish(new ModelPublishedEvent(result, trainEvt.CorrelationId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{EngineName}]: Failed to handle TrainAndPublishEvent", nameof(PredictionEngine));
            throw;
        }
    }
}