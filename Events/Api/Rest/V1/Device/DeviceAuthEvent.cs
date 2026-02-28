using grefurBackend.Models;
using grefurBackend.Events;

namespace grefurBackend.Events.Device;

public enum DeviceAuthStatus
{
    NeedAuth,
    Authenticated,
    AuthFailed
}

public sealed class DeviceAuthEvent : Event
{
    public string CustomerId { get; init; }
    public string DeviceId { get; init; }
    public DeviceAuthStatus AuthStatus { get; init; }

    public DeviceAuthEvent(
        string source,
        string correlationId,
        string customerId,
        string deviceId,
        DeviceAuthStatus authStatus
    ) : base(
        eventType: nameof(DeviceAuthEvent),
        source: source,
        correlationId: correlationId,
        payload: new { customerId, deviceId, authStatus })
    {
        this.CustomerId = customerId;
        this.DeviceId = deviceId;
        this.AuthStatus = authStatus;
    }
}