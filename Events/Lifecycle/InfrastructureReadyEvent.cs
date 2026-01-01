namespace grefurBackend.Events.Lifecycle;

public sealed class InfrastructureReadyEvent : LifecycleEvent
{
    public InfrastructureReadyEvent(string source, string correlationId)
        : base(
            eventType: "InfrastructureReady",
            source: source,
            correlationId: correlationId,
            payload: new { })
    {
    }
}
