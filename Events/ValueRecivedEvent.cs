namespace grefurBackend.Events;

public sealed class ValueReceivedEvent : Event
{
    public string DeviceId { get; }
    public string ValueType { get; }
    public string Value { get; }
    public string Topic { get; }

    // Primary constructor (all values explicitly provided)
    public ValueReceivedEvent(
        string DeviceId,
        string ValueType,
        string Value,
        string Source,
        string CorrelationId,
        string Topic)
        : base(
            eventType: "ValueReceived",
            source: Source,
            correlationId: CorrelationId,
            payload: new { DeviceId, ValueType, Value })
    {
        this.DeviceId = DeviceId;
        this.ValueType = ValueType;
        this.Value = Value;
        this.Topic = Topic;
    }

    // Overload for components that only have the topic and raw value
    public ValueReceivedEvent(
        string Source,
        string CorrelationId,
        string DeviceId,
        string Topic,
        string Value)
        : base(
            eventType: "ValueReceived",
            source: Source,
            correlationId: CorrelationId,
            payload: new
            {
                DeviceId = DeviceId,
                ValueType = ExtractValueType(Topic),
                Value = Value
            })
    {
        this.Topic = Topic;
        this.DeviceId = DeviceId;
        this.Value = Value;
        this.ValueType = ExtractValueType(Topic);
    }

    private static string ExtractValueType(string Topic)
    {
        if (string.IsNullOrWhiteSpace(Topic)) return "unknown";
        var Parts = Topic.Split('/');
        return Parts.Length > 2 ? Parts[2] ?? "unknown" : "unknown";
    }
}