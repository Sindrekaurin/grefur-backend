using System.Collections.Generic;

namespace grefurBackend.Types.Dto;

/* Summary of function: Data transfer object for Grefur internal staff to define virtual device templates */
public class VirtualDeviceRegistrationRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string FrontendHeader { get; set; } = string.Empty;
    public string ApiProviderUrl { get; set; } = string.Empty;
    public string RequestHeadersJson { get; set; } = string.Empty;
    public string RequestBodyJson { get; set; } = string.Empty;
    public List<SensorMappingDto> SensorMappings { get; set; } = new();
}




/* Summary of function: Standardized response for device registration attempts in the grefur ecosystem */
public class DeviceRegistrationResult
{
    public bool IsSuccess { get; set; }
    public string? DeviceId { get; set; }
    public string? ErrorReason { get; set; }
    public bool WasConflict { get; set; }
    public bool WasNotFound { get; set; }

    /* Summary of function: Factory method for a successful registration result */
    public static DeviceRegistrationResult Success(string deviceId) =>
        new DeviceRegistrationResult { IsSuccess = true, DeviceId = deviceId };

    /* Summary of function: Factory method for a failed registration result with specific error flags */
    public static DeviceRegistrationResult Failure(string reason, bool conflict = false, bool notFound = false) =>
        new DeviceRegistrationResult { IsSuccess = false, ErrorReason = reason, WasConflict = conflict, WasNotFound = notFound };
}

/* Summary of function: DTO representing a single value mapping from an external API to grefur's internal keys */
public class VirtualSensorValueDto
{
    public required string KeyName { get; set; }
    public required string JsonPath { get; set; }
    public string? Unit { get; set; }
}

/* Summary of function: Defines how to extract a specific value from the API response */
public class SensorMappingDto
{
    public string MqttKey { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string DataType { get; set; } = "float";
    public double Multiplier { get; set; } = 1.0;
}