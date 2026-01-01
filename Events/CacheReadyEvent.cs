namespace grefurBackend.Events.Domain;

public sealed class CacheReadyEvent : Event
{
    public string CustomerId { get; }

    public CacheReadyEvent(
        string customerId,
        string source,
        string correlationId)
        : base(
            eventType: "CacheReady",
            source: source,
            correlationId: correlationId,
            payload: new { customerId })
    {
        CustomerId = customerId;
    }
}