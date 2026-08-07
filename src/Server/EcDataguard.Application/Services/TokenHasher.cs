using System.Security.Cryptography;
using System.Text;
using EcDataguard.Domain.Entities;

namespace EcDataguard.Application.Services;

public static class TokenHasher
{
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}

public static class TokenState
{
    public static bool IsActive(DeviceToken t, string token, DateTime now)
        => t.TenantId != Guid.Empty
           && t.TokenHash == TokenHasher.Hash(token)
           && !t.Revoked
           && t.ExpiresUtc > now;
}