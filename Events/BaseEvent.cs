using System;

namespace grefurBackend.Events;

public abstract class Event
{
    public string EventId { get; }
    public string EventType { get; }
    public DateTime Timestamp { get; }
    public string Source { get; }
    public string CorrelationId { get; }
    public object Payload { get; }

    protected Event(
        string eventType,
        string source,
        string correlationId,
        object payload)
    {
        EventId = Guid.NewGuid().ToString();
        EventType = eventType;
        Timestamp = DateTime.UtcNow;
        Source = source;
        CorrelationId = correlationId;
        Payload = payload;
    }
}