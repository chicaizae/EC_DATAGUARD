using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;
using EcDataguard.Domain.Enums;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public AuthController(IAppDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public record LoginRequest(string Email, string Password);

    public record LoginResult(string Token, Guid UserId, string Email, string Role, Guid TenantId, Guid? ScopeTenantId);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _db.ConsoleUsers.FirstOrDefaultAsync(u => u.Email == request.Email && u.Enabled, ct);
        if (user is null) return Unauthorized(new { error = "Credenciales inválidas." });

        var hasher = new PasswordHasher<object>();
        var verification = hasher.VerifyHashedPassword(new object(), user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed) return Unauthorized(new { error = "Credenciales inválidas." });

        user.LastSignInUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var token = _tokens.IssueConsoleToken(user, user.TenantId, user.TenantScopeOverride);
        return Ok(new LoginResult(token, user.Id, user.Email, user.Role.ToString(), user.TenantId, user.TenantScopeOverride));
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = AuthClaims.ConsolePolicy)]
    public IActionResult Me()
    {
        var tenantId = User.GetTenantId();
        var scopeTenant = User.GetScopeTenantId();
        var email = User.FindFirst("email")?.Value ?? string.Empty;
        var role = User.FindFirst(AuthClaims.Role)?.Value ?? string.Empty;
        return Ok(new { email, role, tenantId, scopeTenant, isSuperAdmin = User.IsSuperAdmin() });
    }
}