namespace grefurBackend.Events;

public enum ErrorLevel
{
    Info, // Informational messages
    Warning, // Potential issues that are not immediately harmful
    Critical, // Serious issues that require immediate attention
    SystemFailure, // Failures affecting system functionality
    ServiceBreach // Violations of service level agreements
}

public sealed class ErrorEvent : Event
{
    public string ErrorCode { get; }
    public ErrorLevel Level { get; }
    public string Message { get; }
    public string? ExceptionDetails { get; }

    public ErrorEvent(
        string errorCode,
        ErrorLevel level,
        string message,
        string source,
        string correlationId,
        string? exceptionDetails = null)
        : base(
            eventType: "ErrorLog",
            source: source,
            correlationId: correlationId,
            payload: new
            {
                errorCode,
                level,
                message,
                exceptionDetails
            })
    {
        ErrorCode = errorCode;
        Level = level;
        Message = message;
        ExceptionDetails = exceptionDetails;
    }
}