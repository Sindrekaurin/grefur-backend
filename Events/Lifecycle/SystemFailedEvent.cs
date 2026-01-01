namespace grefurBackend.Events.Lifecycle;

public sealed class SystemFailedEvent : LifecycleEvent
{
    public string Reason { get; }

    public SystemFailedEvent(
        string reason,
        string source,
        string correlationId)
        : base(
            eventType: "SystemFailed",
            source: source,
            correlationId: correlationId,
            payload: new { reason })
    {
        this.Reason = reason;
    }
}
