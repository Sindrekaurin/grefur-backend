using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grefurBackend.Models;

/* Summary of function: Extension of GrefurDevice to support virtual sensors fetching multiple data points from a single API endpoint */
public class VirtualGrefurDevice : GrefurDevice
{
    /* Summary of function: A header string or title used specifically for the frontend UI components */
    [StringLength(200)]
    public string? FrontendHeader { get; set; }

    /* Summary of function: A user-friendly description of the virtual sensor's purpose or location */
    [StringLength(500)]
    public string? Description { get; set; }

    /* Summary of function: The source URL or base endpoint for the data provider */
    [Required]
    public required string ApiProviderUrl { get; set; }

    /* Summary of function: Compatibility field to satisfy required base members if necessary */
    public string ApiProvider { get; set; } = "GenericRest";

    /* Summary of function: JSON string containing specific parameters for the provider (lat, lon, etc.) */
    public string ProviderConfigurationJson { get; set; } = "{}";

    /* Summary of function: JSON string containing HTTP headers required for the API call (e.g., API keys) */
    public string RequestHeadersJson { get; set; } = "{}";

    /* Summary of function: JSON string containing the POST/PUT body template for the API call */
    public string RequestBodyJson { get; set; } = "{}";

    /* Summary of function: Legacy support for custom headers if still referenced in migrations */
    public string CustomHeadersJson { get; set; } = "{}";

    /* Summary of function: Collection of specific data points extracted from the API response */
    public virtual ICollection<VirtualSensorValue> SensorValues { get; set; } = new List<VirtualSensorValue>();

    /* Summary of function: Tracking when the next update should occur based on the refresh interval */
    public DateTime NextScheduledFetchUtc { get; set; }

    /* Summary of function: Constructor to set default values specific to virtual sensors */
    public VirtualGrefurDevice()
    {
        this.DeviceType = "VirtualSensor";
        this.HardwareVersion = "Cloud-Logic-v2";
        this.SoftwareVersion = "1.1.0";
        this.IsNested = true;
    }

    /* Summary of function: Updates the next fetch timestamp based on the HeartbeatIntervalSeconds */
    public void ScheduleNextFetch()
    {
        // Default to 3600 seconds if interval is not set to avoid infinite loops
        int interval = this.HeartbeatIntervalSeconds > 0 ? this.HeartbeatIntervalSeconds : 3600;
        this.NextScheduledFetchUtc = DateTime.UtcNow.AddSeconds(interval);
    }
}

/* Summary of function: Represents a specific value mapping from a virtual device's API response */
public class VirtualSensorValue
{
    [Key]
    public Guid ValueId { get; set; } = Guid.NewGuid();

    [ForeignKey("VirtualGrefurDevice")]
    public required string DeviceId { get; set; }

    /* Summary of function: The key name used in the outgoing MQTT message, e.g., 'temperature' */
    [Required]
    public required string KeyName { get; set; }

    /* Summary of function: The path to the value in the API JSON response, supports dot notation and arrays */
    [Required]
    public required string JsonPath { get; set; }

    /* Summary of function: The unit of measurement, e.g., '°C', '%', 'NOK/kWh' */
    public string? Unit { get; set; }

    /* Summary of function: The target data type for casting (float, int, bool, string) */
    public string DataType { get; set; } = "float";

    /* Summary of function: Mathematical scaling factor applied to the raw numeric value */
    public double Multiplier { get; set; } = 1.0;

    /* Summary of function: The last successfully fetched value for this specific key */
    public string? LastValue { get; set; }
}