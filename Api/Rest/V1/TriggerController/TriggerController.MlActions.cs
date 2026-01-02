using grefurBackend.Events;
using grefurBackend.Models.AlarmConfiguration;

namespace grefurBackend.Api.Rest.V1
{
    public partial class TriggerController
    {
        private async Task<object> HandleTrainAndPublishAsync(string correlationId)
        {
            var config = new MlAlarmConfiguration
            {
                CustomerId = "CUST-001",
                TargetMeasurementId = "Grefur_3461/900/320/001/RT401/value",
                FeatureMeasurementIds = new List<string> { "Grefur_235cfe/900/320/001/RT901/value" },
                TrainingFrequency = TrainingFrequency.Weekly,
                SampleIntervalMinutes = 5
            };

            _logger.LogInformation("TriggerController: Publishing TrainAndPublishEvent for sensor {SensorId}", config.TargetMeasurementId);

            var trainEvent = new TrainAndPublishEvent(config, "TriggerController", correlationId);
            await _eventBus.Publish(trainEvent);

            return new { Event = "TrainAndPublish", Status = "Triggered", CorrelationId = correlationId };
        }
    }
}