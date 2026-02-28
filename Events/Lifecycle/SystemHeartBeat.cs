namespace grefurBackend.Events.Lifecycle;

public sealed class SystemHeartBeat : LifecycleEvent
{
    public SystemHeartBeat(string source, string correlationId, object payload)
        : base(
            eventType: "SystemHeartBeat",
            source: source,
            correlationId: correlationId,
            payload: payload)
    {
    }
}