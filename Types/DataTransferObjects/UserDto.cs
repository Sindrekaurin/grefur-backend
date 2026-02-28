namespace grefurBackend.Types.Dtos;

public class UserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? MetadataJson { get; set; }
    public string? PreferredLanguage { get; set; }
    public bool ForcePasswordChange { get; set; }
}