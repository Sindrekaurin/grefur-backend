using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using grefurBackend.Context;
using grefurBackend.Models;
using grefurBackend.Types;
using BCrypt.Net;

namespace grefurBackend.Services;

public class UserService
{
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly ILogger<UserService> _logger;

    public UserService(IDbContextFactory<MySqlContext> contextFactory, ILogger<UserService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // {Retrieves a full user profile by ID}
    public async Task<GrefurUser?> GetUserByIdAsync(string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GrefurUsers
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    // {Handles the creation of new users with mandatory BCrypt hashing and default password logic}
    public async Task<(bool success, string message, string? userId)> CreateUserAsync(GrefurUser newUser)
    {
        // {SECURITY: Prevents creating SystemAdmin for external customers}
        if (newUser.Role == UserRole.SystemAdmin && newUser.CustomerId != "GREFUR-INTERNAL")
        {
            _logger.LogWarning("[UserService]: Blocked attempt to create SystemAdmin for external customer: {Email}", newUser.Email);
            return (false, "Only internal Grefur users can be SystemAdmins", null);
        }

        using var context = await _contextFactory.CreateDbContextAsync();

        if (await context.GrefurUsers.AnyAsync(u => u.Email == newUser.Email))
        {
            return (false, "A user with this email already exists", null);
        }

        if (newUser.Role != UserRole.SystemAdmin)
        {
            var customerExists = await context.GrefurCustomers.AnyAsync(c => c.CustomerId == newUser.CustomerId);
            if (!customerExists)
            {
                return (false, "The specified Customer ID does not exist", null);
            }
        }

        newUser.UserId = Guid.NewGuid().ToString();
        newUser.CreatedAt = DateTime.UtcNow;
        newUser.IsEnabled = true;

        // {DEFAULT PASSWORD LOGIC: Set to "admin" if no password provided}
        string passwordToHash = string.IsNullOrEmpty(newUser.PasswordHash) ? "admin" : newUser.PasswordHash;

        if (passwordToHash == "admin")
        {
            newUser.ForcePasswordChange = true;
            _logger.LogInformation("[UserService]: Using default password 'admin' for {Email}", newUser.Email);
        }

        newUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordToHash);

        context.GrefurUsers.Add(newUser);
        await context.SaveChangesAsync();

        _logger.LogInformation("[UserService]: New user {Email} created for customer {CustomerId}", newUser.Email, newUser.CustomerId);

        return (true, "User created successfully", newUser.UserId);
    }

    // {NEW: Handles updates to existing users while maintaining EF Core tracking integrity}
    public async Task<bool> UpdateUserAsync(GrefurUser updatedUser)
    {
        _logger.LogInformation("[UserService]: UpdateUserAsync started for Email: {Email}", updatedUser.Email);

        using var context = await _contextFactory.CreateDbContextAsync();

        // Debug log to see what we are searching for
        var existingUser = await context.GrefurUsers
            .FirstOrDefaultAsync(u => u.UserId == updatedUser.UserId || u.Email == updatedUser.Email);

        if (existingUser == null)
        {
            _logger.LogWarning("[UserService]: No user found in DB matching ID: {UserId} or Email: {Email}",
                updatedUser.UserId, updatedUser.Email);
            return false;
        }

        _logger.LogInformation("[UserService]: Found existing user {Email} (ID: {UserId}). Mapping values...",
            existingUser.Email, existingUser.UserId);

        // Apply changes
        existingUser.Email = updatedUser.Email;
        existingUser.Role = updatedUser.Role;
        existingUser.CustomerId = updatedUser.CustomerId;
        existingUser.IsEnabled = updatedUser.IsEnabled;
        existingUser.PreferredLanguage = updatedUser.PreferredLanguage;
        existingUser.MetadataJson = updatedUser.MetadataJson;
        existingUser.ForcePasswordChange = updatedUser.ForcePasswordChange;

        if (!string.IsNullOrEmpty(updatedUser.PasswordHash) && updatedUser.PasswordHash != "RESET_REQUIRED")
        {
            existingUser.PasswordHash = updatedUser.PasswordHash;
        }

        try
        {
            var result = await context.SaveChangesAsync();
            _logger.LogInformation("[UserService]: SaveChangesAsync returned {Count} rows affected for {Email}", result, existingUser.Email);

            // Return true even if result is 0 (meaning nothing changed in DB, but record exists)
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService]: Exception during SaveChangesAsync for {Email}", updatedUser.Email);
            return false;
        }
    }

    // {Updates user language preference for localized sensor dashboard}
    public async Task<bool> UpdateUserLanguageAsync(string userId, string languageCode)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.GrefurUsers.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null) return false;

        user.PreferredLanguage = languageCode;
        await context.SaveChangesAsync();

        return true;
    }

    // {Removes a user while ensuring system integrity}
    public async Task<bool> RemoveUserAsync(string userId, string performingUserId)
    {
        if (userId == performingUserId) return false;

        using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.GrefurUsers.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null) return false;

        context.GrefurUsers.Remove(user);
        await context.SaveChangesAsync();

        _logger.LogWarning("[UserService]: User {UserId} was deleted by {AdminId}", userId, performingUserId);
        return true;
    }

    // {Retrieves organizational context for the user}
    public async Task<GrefurCustomer?> GetUserOrganizationAsync(string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.GrefurUsers.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null || string.IsNullOrEmpty(user.CustomerId)) return null;

        return await context.GrefurCustomers
            .FirstOrDefaultAsync(c => c.CustomerId == user.CustomerId);
    }
}