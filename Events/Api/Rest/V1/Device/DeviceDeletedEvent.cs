
namespace grefurBackend.Events.Device;

public sealed class DeviceDeletedEvent : Event
{
    public string CustomerId { get; init; }
    public string DeviceId { get; init; }

    public DeviceDeletedEvent(
        string Source,
        string CorrelationId,
        string CustomerId,
        string DeviceId)
        : base(
            eventType: nameof(DeviceDeletedEvent),
            source: Source,
            correlationId: CorrelationId,
            payload: new { CustomerId, DeviceId })
    {
        this.CustomerId = CustomerId;
        this.DeviceId = DeviceId;
    }
}