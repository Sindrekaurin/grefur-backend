using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grefurBackend.Models;

[AttributeUsage(AttributeTargets.Field)]
public class RoleMetadataAttribute : Attribute
{
    public string Description { get; }
    public int AccessLevel { get; }

    public RoleMetadataAttribute(string description, int accessLevel)
    {
        Description = description;
        AccessLevel = accessLevel;
    }
}

public enum UserRole
{
    [RoleMetadata("Global system administrator for Grefur operations", 100)]
    SystemAdmin,

    [RoleMetadata("Customer organization administrator", 50)]
    Admin,

    [RoleMetadata("Standard user with full operational access", 10)]
    User,

    [RoleMetadata("Read-only access to sensors and logs", 1)]
    Viewer
}

public class GrefurUser
{
    [Key]
    public string UserId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? CustomerId { get; set; }

    [Required]
    public UserRole Role { get; set; } = UserRole.User;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLogin { get; set; }

    [Column(TypeName = "json")]
    public string MetadataJson { get; set; } = "{}";

    public string? PreferredLanguage { get; set; } = "en";

    [Required]
    public bool ForcePasswordChange { get; set; } = true;
}