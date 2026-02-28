namespace grefurBackend.Events;

public sealed class UnknownValueEvent : Event
{
    public string Topic { get; }

    public UnknownValueEvent(
        string Topic,
        string Source,
        string CorrelationId
    ) : base(
        eventType: "UnknownValue",
        source: Source,
        correlationId: CorrelationId,
        payload: new { Topic }
    )
    {
        this.Topic = Topic;
    }
}