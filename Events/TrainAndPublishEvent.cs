using grefurBackend.Events;
using System;
using grefurBackend.Models.AlarmConfiguration;

namespace grefurBackend.Events
{
    public sealed class TrainAndPublishEvent : Event
    {
        public MlAlarmConfiguration Configuration { get; }

        public TrainAndPublishEvent(
            MlAlarmConfiguration configuration,
            string source,
            string correlationId)
            : base(
                eventType: "TrainAndPublish",
                source: string.IsNullOrWhiteSpace(source) ? throw new ArgumentException("source must be provided") : source,
                correlationId: string.IsNullOrWhiteSpace(correlationId) ? throw new ArgumentException("correlationId must be provided") : correlationId,
                payload: configuration ?? throw new ArgumentNullException(nameof(configuration)))
        {
            Configuration = configuration;
        }
    }
}
