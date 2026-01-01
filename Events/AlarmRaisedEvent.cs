using grefurBackend.Events;

namespace grefurBackend.Events.Domain;

public class AlarmRaisedEvent : Event
{
    public string CustomerId { get; init; }
    public string DeviceId { get; init; }
    public string Topic { get; init; }
    public double Value { get; init; }
    public string Message { get; init; }

    public AlarmRaisedEvent(
        string Source,
        string CorrelationId,
        string CustomerId,
        string DeviceId,
        string Topic,
        double Value,
        string Message)
        : base(
            eventType: nameof(AlarmRaisedEvent),
            source: Source,
            correlationId: CorrelationId,
            payload: new { CustomerId, DeviceId, Topic, Value, Message })
    {
        this.CustomerId = CustomerId;
        this.DeviceId = DeviceId;
        this.Topic = Topic;
        this.Value = Value;
        this.Message = Message;
    }
}