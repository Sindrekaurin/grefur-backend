namespace grefurBackend.Types;

public class LoginRequest
{
    public required string email { get; set; }
    public required string password { get; set; }
    public bool redirectTrue { get; set; }
    public string? href { get; set; }
}

public class RegisterRequest
{
    public required string email { get; set; } = string.Empty;
    public required string password { get; set; } = string.Empty;
    public string customerId { get; set; } = string.Empty;
}

public record ChangePasswordRequest(string newPassword);