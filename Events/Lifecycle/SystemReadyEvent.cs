namespace grefurBackend.Events.Lifecycle;

public sealed class SystemReadyEvent : LifecycleEvent
{
    public SystemReadyEvent(string source, string correlationId)
        : base(
            eventType: "SystemReady",
            source: source,
            correlationId: correlationId,
            payload: new { })
    {
    }
}
