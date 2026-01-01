namespace grefurBackend.Events.Domain;

public sealed class DeviceRegisteredEvent : Event
{
    public string CustomerId { get; }
    public string DeviceId { get; }

    public DeviceRegisteredEvent(
        string customerId,
        string deviceId,
        string source,
        string correlationId)
        : base(
            eventType: "DeviceRegistered",
            source: source,
            correlationId: correlationId,
            payload: new { customerId, deviceId })
    {
        CustomerId = customerId;
        DeviceId = deviceId;
    }
}