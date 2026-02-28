using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace grefurBackend.Models;

[Table("sensorReadings")]
public class SensorReading
{
    public DateTime Timestamp { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public double Value { get; set; }
}