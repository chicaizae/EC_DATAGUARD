using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "ecdataguard";
    public string Audience { get; set; } = "ecdataguard-console";
    public string Secret { get; set; } = "ChangeMeInProduction_BigRandomValue_0123456789";
    public TimeSpan ConsoleTokenLifetime { get; set; } = TimeSpan.FromHours(8);
}

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options) => _options = options;

    public string IssueConsoleToken(ConsoleUser user, Guid tenantId, Guid? scopeTenantId)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("tenant_id", tenantId.ToString()),
            new("role", user.Role.ToString()),
            new("scope", "console"),
        };
        if (scopeTenantId.HasValue)
        {
            claims.Add(new Claim("scope_tenant_id", scopeTenantId.Value.ToString()));
        }

        return WriteToken(claims, _options.ConsoleTokenLifetime);
    }

    public string IssueDeviceToken(Guid tenantId, Guid deviceId, TimeSpan expiresIn)
    {
        var claims = new List<Claim>
        {
            new("sub", deviceId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new("scope", "agent")
        };
        return WriteToken(claims, expiresIn);
    }

    private string WriteToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}