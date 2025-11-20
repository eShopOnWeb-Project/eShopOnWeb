using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.eShopWeb.Infrastructure.Authentication;
public class TokenService
{
    private readonly string _secretKey;
    private readonly ILogger<TokenService> _logger;

    public TokenService(string secretKey, ILogger<TokenService> logger)
    {
        _secretKey = secretKey;
        _logger = logger;
    }

    public string GenerateToken()
    {
        _logger.LogInformation("Generating gateway token.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", "webapp"),
            new Claim("aud", "gateway_api"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        var token = new JwtSecurityToken(
            issuer: "myapp",
            audience: "gateway_api",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("Gateway token created with expiration at {ExpiresAt:u}.", expiresAt);

        return serializedToken;
    }
}

