
using System.Text.Json.Serialization;
namespace grefurBackend.Types.Dto;

/* Summary of model: Request object for device authentication */
public class DeviceAuthRequest
{
    [JsonPropertyName("ServiceValidationToken")]
    public string ServiceValidationToken { get; set; } = string.Empty;
}

