namespace grefurBackend.Events.Device;

public class DeviceOperationResult
{
    public bool IsSuccess { get; set; }
    public string? DeviceId { get; set; }
    public string? ErrorReason { get; set; }
    public bool WasNotFound { get; set; }
    public bool WasConflict { get; set; }

    public static DeviceOperationResult Success(string deviceId) => new() { IsSuccess = true, DeviceId = deviceId };
    public static DeviceOperationResult Conflict(string reason) => new() { WasConflict = true, ErrorReason = reason };
    public static DeviceOperationResult NotFound(string reason) => new() { WasNotFound = true, ErrorReason = reason };
}