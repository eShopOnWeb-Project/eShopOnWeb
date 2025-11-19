using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.eShopWeb.Infrastructure.Authentication;
public class TokenService
{
    private readonly string _secretKey;

    public TokenService(string secretKey)
    {
        _secretKey = secretKey;
    }

    public string GenerateToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", "webapp"),
            new Claim("aud", "gateway_api"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "myapp",
            audience: "gateway_api",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

