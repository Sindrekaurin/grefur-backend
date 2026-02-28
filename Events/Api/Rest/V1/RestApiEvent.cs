using System;

namespace grefurBackend.Events.Integration;

public class RestApiEvent : Event
{
    public string Endpoint { get; }
    public string Method { get; }
    public string DeviceId { get; }

    public RestApiEvent(
        string source,
        string correlationId,
        string endpoint,
        string method,
        string deviceId)
        : base(
            eventType: nameof(RestApiEvent),
            source: source,
            correlationId: correlationId,
            payload: new { endpoint, method, deviceId })
    {
        Endpoint = endpoint;
        Method = method;
        DeviceId = deviceId;
    }
}