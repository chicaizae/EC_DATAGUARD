using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;
using EcDataguard.Application.Services;
using EcDataguard.Domain.Enums;
using EcDataguard.Domain.Entities;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/console/tenants")]
public class TenantController : BaseConsoleController
{
    private readonly ITenantAdminService _tenants;
    private readonly ILicenseService _licenses;

    public TenantController(ITenantAdminService tenants, ILicenseService licenses)
    {
        _tenants = tenants;
        _licenses = licenses;
    }

    /// <summary>Devuelve todas las empresas (superadmin) o la propia (tenant).</summary>
    [Authorize(Policy = AuthClaims.ConsolePolicy)]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var rows = await _tenants.ListAsync(ct);
        var result = new List<object>();
        foreach (var t in rows)
        {
            var license = await _licenses.GetSnapshotAsync(t.Id, ct);
            result.Add(new
            {
                t.Id,
                t.Codigo,
                t.Nombre,
                Plan = t.Plan.ToString(),
                t.Activo,
                Dispositivos = license?.Devices ?? 0,
                LicensedUserLimit = license?.LicensedUserLimit ?? 0,
                ActiveUsers = license?.ActiveUsers ?? 0,
                LicensedUsers = license?.LicensedUsers ?? 0,
                OverLimit = license?.OverLimit ?? false,
                UsagePercent = license?.UsagePercent ?? 0
            });
        }
        return Ok(result);
    }

    [Authorize(Policy = AuthClaims.ConsolePolicy)]
    [HttpGet("{tenantId:guid}/license")]
    public async Task<IActionResult> License(Guid tenantId, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        var snapshot = await _licenses.GetSnapshotAsync(tenantId, ct);
        return snapshot is null ? NotFound() : Ok(new
        {
            snapshot.TenantId,
            Plan = snapshot.Plan.ToString(),
            snapshot.LicensedUserLimit,
            snapshot.ActiveUsers,
            snapshot.LicensedUsers,
            snapshot.Devices,
            snapshot.OverLimit,
            snapshot.UsagePercent
        });
    }

    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Nombre))
        {
            return BadRequest(new { message = "Codigo y Nombre son obligatorios." });
        }

        var tenant = await _tenants.CreateAsync(request.Codigo, request.Nombre, request.Plan, ct);
        return Ok(new
        {
            tenant.Id,
            tenant.Codigo,
            tenant.Nombre,
            tenant.Plan,
            tenant.Activo
        });
    }

    public record CreateTenantRequest(string Codigo, string Nombre, TenantPlan Plan = TenantPlan.Enterprise);
}
