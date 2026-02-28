using System;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using grefurBackend.Models;
using grefurBackend.Types;
using grefurBackend.Context;
using grefurBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using DotNetEnv;
using grefurBackend.Helpers;

namespace grefurBackend.Controllers.Api.Rest.V1.Auth;

[ApiController]
[Route("api/rest/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IDbContextFactory<MySqlContext> _contextFactory;
    private readonly UserService _userService;
    private readonly MqttService _mqttService;

    public AuthController(
        ILogger<AuthController> logger,
        IDbContextFactory<MySqlContext> contextFactory,
        UserService userService,
        MqttService mqttService)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _userService = userService;
        _mqttService = mqttService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {

        Console.WriteLine("Login attempt for user: " + request.email);

        using var context = await _contextFactory.CreateDbContextAsync();

        var user = await context.GrefurUsers.FirstOrDefaultAsync(u => u.Email == request.email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.password, user.PasswordHash))
        {
            _logger.LogWarning("[Auth] Failed login attempt for user: {User}", request.email);
            return Unauthorized(new { message = "Incorrect email or password" });
        }

        var customer = await _userService.GetUserOrganizationAsync(user.UserId);

        if (user.Role != UserRole.SystemAdmin && (customer == null || !customer.IsEnabled))
        {
            return StatusCode(403, new { message = "Organisasjonen er deaktivert eller finnes ikke." });
        }

        return await SignInUser(user, customer, request, user.ForcePasswordChange);
    }

    [HttpPost("password-change")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.GrefurUsers.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null) return NotFound();

        if (request.newPassword.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.newPassword);
        user.ForcePasswordChange = false;

        context.GrefurUsers.Update(user);
        await context.SaveChangesAsync();

        _logger.LogInformation("[Auth] Password changed successfully for user {UserId}", userId);

        var customer = await _userService.GetUserOrganizationAsync(user.UserId);

        return await SignInUser(user, customer, new LoginRequest { email = user.Email!, password = user.PasswordHash, redirectTrue = true }, false);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var newUser = new GrefurUser
        {
            Email = request.email,
            PasswordHash = request.password,
            CustomerId = request.customerId,
            Role = UserRole.Admin
        };

        var (success, message, userId) = await _userService.CreateUserAsync(newUser);

        if (!success) return BadRequest(new { message });

        _logger.LogInformation("[Auth] New user registered via API: {UserId}", userId);
        return Ok(new { success = true, message = "User created successfully" });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await _mqttService.DeleteBrokerUserAsync(userId);
            _logger.LogInformation("[Auth] Cleaned up MQTT user for {UserId}", userId);
        }

        Response.Cookies.Delete("grefur_auth", new CookieOptions { Path = "/" });
        return Ok(new { success = true });
    }

    /* Summary of function: Updates broker password and re-issues JWT to keep client state in sync */
    [Authorize]
    [HttpGet("renew/broker/credentials")]
    public async Task<IActionResult> NewBrokerConnection()
    {
        // Bruk null-coalescing (??) for å sikre at vi har verdier eller tomme strenger
        var userId = User.FindFirst("userId")?.Value;
        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var customerId = User.FindFirst("customerId")?.Value ?? string.Empty;
        var roleStr = User.FindFirst(ClaimTypes.Role)?.Value;

        // Hvis userId mangler, er tokenet korrupt eller ugyldig
        if (string.IsNullOrEmpty(userId) || !Enum.TryParse<UserRole>(roleStr, out var role))
        {
            return Unauthorized();
        }

        var mqttPassword = Guid.NewGuid().ToString("N");

        try
        {
            // 1. Update the physical broker access
            await _mqttService.DeleteBrokerUserAsync(userId);
            await _mqttService.CreateBrokerUserAsync(userId, mqttPassword);

            // 2. Prepare new claims with the NEW password
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, userEmail),
                new Claim("userId", userId),
                new Claim("customerId", customerId),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("mqttUser", userId),
                new Claim("mqttPass", mqttPassword)
            };

            // 3. Generate a fresh token. 
            // Vi bruker "!" (null-forgiving operator) her fordi vi vet userId ikke er null pga sjekken over.
            var token = JwtHelper.GenerateJwtToken(
                new GrefurUser { UserId = userId, Email = userEmail },
                new GrefurCustomer { CustomerId = customerId },
                claims,
                false);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Keep false for local IP/mobile testing
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(7),
                Path = "/"
            };

            // 4. Overwrite the old cookie
            Response.Cookies.Append("grefur_auth", token, cookieOptions);

            _logger.LogInformation("[Auth] Re-issued JWT with new MQTT credentials for {UserId}", userId);

            return Ok(new
            {
                success = true,
                mqtt = new { username = userId, password = mqttPassword }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Auth] Failed to sync new broker connection for {UserId}", userId);
            return StatusCode(500, new { message = "Sync failed." });
        }
    }

    private async Task<IActionResult> SignInUser(GrefurUser user, GrefurCustomer? customer, LoginRequest request, bool isPasswordChangeRequired)
    {
        var effectiveCustomer = customer ?? new GrefurCustomer
        {
            CustomerId = "GREFUR-INTERNAL",
            OrganizationName = "Grefur Internal"
        };

        var mqttPassword = Guid.NewGuid().ToString("N");
        await _mqttService.DeleteBrokerUserAsync(user.UserId!);
        await _mqttService.CreateBrokerUserAsync(user.UserId!, mqttPassword);

        Console.WriteLine("MQTT user password:");
        Console.WriteLine(mqttPassword);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim("userId", user.UserId ?? ""),
            new Claim("customerId", user.CustomerId ?? ""),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("forcePasswordChange", isPasswordChangeRequired.ToString().ToLower()),

            // MQTT session user
            new Claim("mqttUser", user.UserId ?? ""),
            new Claim("mqttPass", mqttPassword)
        };

        var token = JwtHelper.GenerateJwtToken(user, effectiveCustomer, claims, isPasswordChangeRequired);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = isPasswordChangeRequired ? DateTime.UtcNow.AddMinutes(15) : DateTime.UtcNow.AddDays(7),
            Path = "/"
        };

        Response.Cookies.Append("grefur_auth", token, cookieOptions);

        string redirectTo;
        if (isPasswordChangeRequired)
        {
            redirectTo = "/login/change/password";
        }
        else if (!string.IsNullOrEmpty(request.href))
        {
            redirectTo = request.href
                .Replace("[user.CustomerId]", user.CustomerId ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("[customer.OrganizationName]", effectiveCustomer.OrganizationName ?? "", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            redirectTo = user.Role == UserRole.SystemAdmin ? "/admin" : "/datalake";
        }

        return Ok(new
        {
            success = true,
            requiresPasswordChange = isPasswordChangeRequired,
            customerId = effectiveCustomer.CustomerId,
            role = user.Role.ToString(),
            redirectTo = redirectTo,
            mqtt = new
            {
                username = user.UserId,
                password = mqttPassword
            }
        });
    }
}

   