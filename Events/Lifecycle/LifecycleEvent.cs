namespace grefurBackend.Events.Lifecycle;

public abstract class LifecycleEvent : Event
{
    protected LifecycleEvent(
        string eventType,
        string source,
        string correlationId,
        object payload)
        : base(eventType, source, correlationId, payload)
    {
    }
}
