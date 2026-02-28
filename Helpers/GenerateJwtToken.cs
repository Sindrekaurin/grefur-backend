using System;
using System.Text;
using System.Collections.Generic;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using grefurBackend.Models;

namespace grefurBackend.Helpers;

/* Summary of function: Helper class to centralize JWT generation for users and IoT devices */
public static class JwtHelper
{
    public static string GenerateJwtToken(GrefurUser user, GrefurCustomer customer, List<Claim> claims, bool isPasswordChangeRequired)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        // Henter key fra Env - viktig for sikkerhet i produksjon på Raspberry Pi
        var keyString = Env.GetString("JWT_KEY") ?? "Grefur_Super_Secret_Base_Key_2026_Startup";
        var key = Encoding.ASCII.GetBytes(keyString);

        // En enkel sjekk for å se om kunden er validert (CreatedAt settes ved aktivering)
        var expirationDays = (customer != null && customer.CreatedAt != DateTime.MinValue) ? 7 : 1;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = isPasswordChangeRequired
                ? DateTime.UtcNow.AddMinutes(15)
                : DateTime.UtcNow.AddDays(expirationDays),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /* Summary of function: Specialized token generation for IoT devices with custom expiry */
    public static string GenerateDeviceJwt(List<Claim> claims, int expiresHours = 24)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyString = Env.GetString("JWT_KEY") ?? "Grefur_Super_Secret_Base_Key_2026_Startup";
        var key = Encoding.ASCII.GetBytes(keyString);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(expiresHours),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}