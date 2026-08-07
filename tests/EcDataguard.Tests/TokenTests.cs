using EcDataguard.Application.Services;
using EcDataguard.Domain.Entities;
using Xunit;

namespace EcDataguard.Tests;

public class TokenTests
{
    [Fact]
    public void Hash_EsDeterministaYDe64Hex()
    {
        var a = TokenHasher.Hash("eyJhbGciOiJIUzI1NiJ9.payload.signature");
        var b = TokenHasher.Hash("eyJhbGciOiJIUzI1NiJ9.payload.signature");

        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void Hash_TokensDistintosDiferen()
    {
        Assert.NotEqual(
            TokenHasher.Hash("token-A"),
            TokenHasher.Hash("token-B"));
    }

    [Fact]
    public void IsActive_DevuelveTrueConRowValido()
    {
        var now = DateTime.UtcNow;
        var row = new DeviceToken
        {
            TenantId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            TokenHash = TokenHasher.Hash("tok"),
            ExpiresUtc = now.AddDays(1),
            Revoked = false
        };

        Assert.True(TokenState.IsActive(row, "tok", now));
    }

    [Fact]
    public void IsActive_RevocadoOExpiradoDevuelveFalse()
    {
        var now = DateTime.UtcNow;
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var revoked = new DeviceToken { TenantId = tenantId, DeviceId = deviceId, TokenHash = TokenHasher.Hash("tok"), ExpiresUtc = now.AddDays(1), Revoked = true };
        var expired = new DeviceToken { TenantId = tenantId, DeviceId = deviceId, TokenHash = TokenHasher.Hash("tok"), ExpiresUtc = now.AddMinutes(-1), Revoked = false };
        var wrongHash = new DeviceToken { TenantId = tenantId, DeviceId = deviceId, TokenHash = TokenHasher.Hash("otro"), ExpiresUtc = now.AddDays(1), Revoked = false };
        var emptyTenant = new DeviceToken { TenantId = Guid.Empty, DeviceId = deviceId, TokenHash = TokenHasher.Hash("tok"), ExpiresUtc = now.AddDays(1), Revoked = false };

        Assert.False(TokenState.IsActive(revoked, "tok", now));
        Assert.False(TokenState.IsActive(expired, "tok", now));
        Assert.False(TokenState.IsActive(wrongHash, "tok", now));
        Assert.False(TokenState.IsActive(emptyTenant, "tok", now));
    }
}