using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grefurBackend.Models;



public class GrefurDevice
{
    [Key]
    public required string DeviceId { get; set; }

    [Required]
    public required string CustomerId { get; set; }

    public required string DeviceName { get; set; }

    public required string DeviceType { get; set; }

    [Required]
    public required string SoftwareVersion { get; set; }

    public required string HardwareVersion { get; set; }

    public bool IsNested { get; set; } = false;

    public DateTime LastSignOfLife { get; set; }

    public int HeartbeatIntervalSeconds { get; set; } = 300;

    public required string MetadataJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CustomerId")]
    public virtual GrefurCustomer Customer { get; set; } = null!;

    public bool IsDeletedByCustomer { get; set; } = false;

    /* Authentication and Network fields for Grefur Hardware communication */

    /* The public IP or Hostname where the device can be reached if it acts as a server */
    public string? RemoteAddress { get; set; }

    /* A unique token generated under production, unchangeable to secure correct device */
    public string? ServiceValidationToken { get; set; }

    /* The thumbprint of the SSL/TLS certificate the device should expect from the Grefur Broker */
    public string? ExpectedServerCertificateThumbprint { get; set; }
}