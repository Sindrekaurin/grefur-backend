using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Context;
using grefurBackend.Models;
using grefurBackend.Services;
using grefurBackend.Helpers;
using grefurBackend.Types.Dtos;
using System.Security.Claims;

namespace grefurBackend.Controllers.Api.Rest.V1.Users;

[Authorize]
[ApiController]
[Route("api/rest/v1/users")]
public class UserController : ControllerBase
{
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly UserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IDbContextFactory<MySqlContext> contextFactory,
        UserService userService,
        ILogger<UserController> logger)
    {
        _contextFactory = contextFactory;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> GetAllUsers()
    {
        using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

        var users = await context.GrefurUsers
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                Email = u.Email,
                Role = u.Role.ToString(),
                CustomerId = u.CustomerId,
                IsEnabled = u.IsEnabled,
                CreatedAt = u.CreatedAt,
                MetadataJson = u.MetadataJson,
                PreferredLanguage = u.PreferredLanguage,
                ForcePasswordChange = u.ForcePasswordChange
            })
            .ToListAsync()
            .ConfigureAwait(false);

        _logger.LogInformation("SystemAdmin retrieved all users. Count: {Count}", users.Count);
        return Ok(users);
    }

    [HttpPost("add")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> AddOrUpdateUser([FromBody] UserDto userDto)
    {
        _logger.LogInformation("[Controller]: AddOrUpdateUser triggered for Email: {Email}, UserId: {UserId}",
            userDto?.Email, userDto?.UserId);

        if (userDto == null || string.IsNullOrEmpty(userDto.Email))
        {
            _logger.LogWarning("[Controller]: Aborting. userDto is null or Email is empty.");
            return BadRequest(new { message = "Invalid user data" });
        }

        if (!Enum.TryParse<UserRole>(userDto.Role, out var roleEnum))
        {
            _logger.LogInformation("[Controller]: Role parsing failed for {Role}, defaulting to User", userDto.Role);
            roleEnum = UserRole.User;
        }

        var userToProcess = new GrefurUser
        {
            UserId = userDto.UserId ?? string.Empty,
            Email = userDto.Email ?? string.Empty,
            Role = roleEnum,
            IsEnabled = userDto.IsEnabled,
            PreferredLanguage = userDto.PreferredLanguage ?? "en",
            ForcePasswordChange = userDto.ForcePasswordChange,
            MetadataJson = userDto.MetadataJson ?? "{}",
            CustomerId = userDto.CustomerId ?? string.Empty
        };

        _logger.LogInformation("[Controller]: Attempting UpdateUserAsync for {Email}...", userToProcess.Email);
        var success = await _userService.UpdateUserAsync(userToProcess).ConfigureAwait(false);

        if (success)
        {
            _logger.LogInformation("[Controller]: UpdateUserAsync reported success for {Email}", userDto.Email);
            return Ok(new { success = true, action = "updated" });
        }

        _logger.LogInformation("[Controller]: Update failed/User not found. Attempting CreateUserAsync for {Email}...", userDto.Email);
        var (createSuccess, message, userId) = await _userService.CreateUserAsync(userToProcess).ConfigureAwait(false);

        if (!createSuccess)
        {
            _logger.LogError("[Controller]: Creation failed for {Email}. Message: {Message}", userDto.Email, message);
            return BadRequest(new { message });
        }

        _logger.LogInformation("[Controller]: Successfully created user {Email} with New ID: {UserId}", userDto.Email, userId);
        return Ok(new { success = true, action = "created", userId });
    }

    [HttpDelete("remove/{userId}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> RemoveUser(string userId)
    {
        // Fixes CS8601 by ensuring currentUserId is not null
        var currentUserId = User.FindFirst("userId")?.Value ?? string.Empty;

        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        var success = await _userService.RemoveUserAsync(userId, currentUserId).ConfigureAwait(false);

        if (!success)
        {
            _logger.LogWarning("[UserController]: Failed to remove user {UserId} requested by {AdminId}", userId, currentUserId);
            return BadRequest(new { message = "Could not remove user. Check if it's your own account or if user exists." });
        }

        return Ok(new { success = true });
    }

    [HttpGet("roles")]
    [Authorize(Roles = "SystemAdmin")]
    public IActionResult GetAvailableRoles()
    {
        var roles = Enum.GetValues(typeof(UserRole))
            .Cast<UserRole>()
            .Select(r => new
            {
                Id = (int)r,
                Name = r.ToString(),
                DisplayName = r.ToString(), // Can be extended with Custom Attributes for cleaner names
                Description = $"Grefur System Role: {r}"
            })
            .ToList();

        _logger.LogInformation("[UserController]: Retrieved {Count} system roles", roles.Count);
        return Ok(roles);
    }
}