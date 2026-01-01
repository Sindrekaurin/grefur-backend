namespace grefurBackend.Events;

public enum LogPointStatus
{
    Requested,
    Received,
    Created,
    Deleted,
    Failed
}

public sealed class LogPointEvent : Event
{
    public string CustomerId { get; }
    public string DeviceId { get; }
    public string Topic { get; }
    public string? ValueType { get; }
    public string? Value { get; }
    public LogPointStatus Status { get; }

    // IsSuccess bestemmes av at status er Created eller Received og at Value ikke er null
    public bool IsSuccess =>
        (Status == LogPointStatus.Received || Status == LogPointStatus.Created)
        && !string.IsNullOrWhiteSpace(Value);

    public LogPointEvent(
        string customerId,
        string deviceId,
        string topic,
        string? valueType,
        string? value,
        LogPointStatus status,
        string source,
        string correlationId)
        : base(
            eventType: "LogPointEvent",
            source: source,
            correlationId: correlationId,
            payload: new
            {
                customerId,
                deviceId,
                topic,
                valueType,
                value,
                status
            })
    {
        CustomerId = customerId;
        DeviceId = deviceId;
        Topic = topic;
        ValueType = valueType;
        Value = value;
        Status = status;
    }
}