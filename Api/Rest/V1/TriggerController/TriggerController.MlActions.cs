using grefurBackend.Events;
using grefurBackend.Models.AlarmConfiguration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace grefurBackend.Api.Rest.V1
{
    public partial class TriggerController
    {
        private async Task<object> HandleTrainAndPublishAsync(string correlationId, Dictionary<string, string> props)
        {
            // Opprett default konfigurasjon
            var config = new MlAlarmConfiguration
            {
                CustomerId = "CUST-001",
                TargetMeasurementId = "Grefur_3461/900/320/001/RT401/value",
                FeatureMeasurementIds = new List<string> { "Grefur_235cfe/900/320/001/RT901/value" },
                TrainingFrequency = TrainingFrequency.Weekly,
                SampleIntervalMinutes = 5,
                ModelVersion = 1 // Sørg for at vi har en versjon for filnavnet
            };

            // Bruk props til å overstyre verdier dynamisk fra API-kallet
            if (props.TryGetValue("customerId", out var customerId)) config.CustomerId = customerId;
            if (props.TryGetValue("targetId", out var targetId)) config.TargetMeasurementId = targetId;
            if (props.TryGetValue("version", out var versionStr) && int.TryParse(versionStr, out var version))
            {
                config.ModelVersion = version;
            }

            _logger.LogInformation("TriggerController: Publishing TrainAndPublishEvent for customer {CustomerId}, target {TargetId}",
                config.CustomerId, config.TargetMeasurementId);

            // Publiser til EventBus slik at MlTrainingService kan plukke det opp asynkront
            var trainEvent = new TrainAndPublishEvent(config, "TriggerController", correlationId);
            await _eventBus.Publish(trainEvent).ConfigureAwait(false);

            return new
            {
                Event = "TrainAndPublish",
                Status = "Queued", // Endret til 'Queued' da det skjer asynkront i bakgrunnen
                Target = config.TargetMeasurementId,
                CorrelationId = correlationId
            };
        }
    }
}