namespace grefurBackend.Events.Domain;

public enum TopicBoundStatus
{
    Success,
    Ignored,
    Failed
}

public sealed class TopicBoundEvent : Event
{
    public string CustomerId { get; }
    public string DeviceId { get; }
    public string BaseTopic { get; }
    public TopicBoundStatus Status { get; }
    public string? StatusMessage { get; }

    public TopicBoundEvent(
        string customerId,
        string deviceId,
        string baseTopic,
        TopicBoundStatus status,
        string source,
        string correlationId,
        string? statusMessage = null)
        : base(
            eventType: "TopicBound",
            source: source,
            correlationId: correlationId,
            payload: new
            {
                customerId,
                deviceId,
                baseTopic,
                status,
                statusMessage
            })
    {
        CustomerId = customerId;
        DeviceId = deviceId;
        BaseTopic = baseTopic;
        Status = status;
        StatusMessage = statusMessage;
    }
}