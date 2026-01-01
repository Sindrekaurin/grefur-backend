namespace grefurBackend.Events.Lifecycle;

public sealed class SystemStartingEvent : LifecycleEvent
{
    public SystemStartingEvent(string source, string correlationId)
        : base(
            eventType: "SystemStarting",
            source: source,
            correlationId: correlationId,
            payload: new { })
    {
    }
}
