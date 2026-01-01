namespace grefurBackend.Events.Integration;

public sealed class MqttMessageReceivedEvent : Event
{
    public string CustomerId { get; }
    public string DeviceId { get; }
    public string ValueType { get; }
    public string RawPayload { get; }
    public string Value { get; }
    public string Topic { get; }

    public MqttMessageReceivedEvent(
        string customerId,
        string deviceId,
        string valueType,
        string rawPayload,
        string value,
        string topic,
        string source,
        string correlationId)
        : base(
            eventType: "MqttMessageReceived",
            source: source,
            correlationId: correlationId,
            payload: new { customerId, deviceId, valueType, rawPayload, value, topic })
    {
        CustomerId = customerId;
        DeviceId = deviceId;
        ValueType = valueType;
        RawPayload = rawPayload;
        Value = value;
        Topic = topic;
    }
}