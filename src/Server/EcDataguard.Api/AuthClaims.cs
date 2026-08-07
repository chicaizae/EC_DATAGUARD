using System.Security.Claims;

namespace EcDataguard.Api;

public static class AuthClaims
{
    public const string Sub = "sub";
    public const string TenantId = "tenant_id";
    public const string ScopeTenantId = "scope_tenant_id";
    public const string Role = "role";
    public const string Scope = "scope";

    public const string AgentScope = "agent";
    public const string ConsoleScope = "console";

    public const string ConsolePolicy = "Console";
    public const string AgentPolicy = "Agent";
    public const string SuperAdminPolicy = "SuperAdmin";
    public const string SuperAdminRole = "SuperAdmin";
}

public static class PrincipalExtensions
{
    public static string? GetName(this ClaimsPrincipal principal, string type)
        => principal.FindFirst(type)?.Value;

    public static string? GetClaim(this ClaimsPrincipal principal, string type)
        => principal.FindFirst(type)?.Value;

    public static Guid? GetTenantId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.GetName(AuthClaims.TenantId), out var id) ? id : null;

    public static Guid? GetScopeTenantId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.GetName(AuthClaims.ScopeTenantId), out var id) ? id : null;

    public static Claim? GetClaimObject(this ClaimsPrincipal principal, string type)
        => principal.FindFirst(type);

    public static bool IsSuperAdmin(this ClaimsPrincipal principal)
        => string.Equals(principal.GetName(AuthClaims.Role), AuthClaims.SuperAdminRole, StringComparison.Ordinal);
}