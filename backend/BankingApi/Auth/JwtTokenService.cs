using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BankingApi.DTOs;
using BankingApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BankingApi.Auth;

public class JwtTokenService
{
    private const string RefreshTokenType = "refresh";

    private readonly JwtSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.Secret))
        {
            throw new InvalidOperationException("JWT secret is not configured.");
        }

        _signingKey = new SymmetricSecurityKey(GetSigningKeyBytes(_settings.Secret));
    }

    public AuthResponse GenerateTokenPair(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        return new AuthResponse
        {
            AccessToken = GenerateAccessToken(user, expiresAt),
            RefreshToken = GenerateRefreshToken(user),
            ExpiresAt = expiresAt
        };
    }

    public Guid? ValidateRefreshToken(string refreshToken)
    {
        var principal = ValidateToken(refreshToken);
        if (principal is null)
        {
            return null;
        }

        if (principal.FindFirst("typ")?.Value != RefreshTokenType)
        {
            return null;
        }

        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    private string GenerateAccessToken(User user, DateTime expiresAt)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return CreateToken(claims, expiresAt);
    }

    private string GenerateRefreshToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddDays(_settings.RefreshExpiryDays);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim("typ", RefreshTokenType),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return CreateToken(claims, expiresAt);
    }

    private string CreateToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(token, GetValidationParameters(), out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    private static byte[] GetSigningKeyBytes(string secret)
    {
        try
        {
            return Convert.FromBase64String(secret);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(secret);
        }
    }
}
