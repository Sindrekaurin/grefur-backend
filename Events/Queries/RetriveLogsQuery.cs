namespace grefurBackend.Events.Queries;

public class RetrieveLogsQuery : Event
{
    public string DeviceId { get; set; } = string.Empty;
    public int Limit { get; set; }
    // Merk: Vi trenger ikke definere CorrelationId her på nytt da den finnes i base-klassen Event

    public RetrieveLogsQuery(string deviceId, int limit, string correlationId)
        : base(
            eventType: "RetrieveLogsQuery",
            source: "TriggerController",
            correlationId: correlationId,
            payload: new { deviceId, limit })
    {
        DeviceId = deviceId;
        Limit = limit;
    }
}

public class RetrieveLogsResponseEvent : Event
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public RetrieveLogsResponseEvent(bool success, string correlationId, object? data = null, string message = "")
        : base(
            eventType: "RetrieveLogsResponse",
            source: "LoggerEngine",
            correlationId: correlationId,
            payload: data ?? new { success, message })
    {
        Success = success;
        Data = data;
        Message = message;
    }
}