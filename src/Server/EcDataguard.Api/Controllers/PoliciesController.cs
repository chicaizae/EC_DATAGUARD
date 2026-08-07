using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;
using EcDataguard.Application.Services;
using EcDataguard.Domain.Enums;
using EcDataguard.Domain.Entities;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/console")]
[Authorize(Policy = AuthClaims.ConsolePolicy)]
public class PoliciesController : BaseConsoleController
{
    private readonly IConsoleQueryService _queries;
    private readonly IAppDbContext _db;
    private readonly IAdminTrailService _trail;

    public PoliciesController(IConsoleQueryService queries, IAppDbContext db, IAdminTrailService trail)
    {
        _queries = queries;
        _db = db;
        _trail = trail;
    }

    [HttpGet("tenants/{tenantId:guid}/policies")]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var policies = await _queries.GetPoliciesAsync(tenantId, ct);
        return Ok(policies.Select(p => new
        {
            p.Id,
            p.Name,
            Kind = p.Kind.ToString(),
            p.Enabled,
            p.Priority,
            Action = p.Action.ToString(),
            p.ConditionsJson,
            p.ScopeJson,
            p.Revision
        }));
    }

    [HttpPost("tenants/{tenantId:guid}/policies")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> Create(Guid tenantId, [FromBody] UpsertPolicyRequest request, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Kind = request.Kind,
            Enabled = request.Enabled,
            Priority = request.Priority,
            Action = request.Action,
            ConditionsJson = request.ConditionsJson ?? "{}",
            ScopeJson = request.ScopeJson ?? "{}",
            InsightTrigger = request.InsightTrigger ?? "Default",
            CreatedUtc = DateTime.UtcNow
        };
        _db.Policies.Add(policy);
        await _db.SaveChangesAsync(ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Policies", $"Creada política: {request.Name}", "{}", ct);
        return Ok(new { policy.Id });
    }

    [HttpPut("tenants/{tenantId:guid}/policies/{policyId:guid}")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> Update(Guid tenantId, Guid policyId, [FromBody] UpsertPolicyRequest request, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == policyId, ct);
        if (policy is null) return NotFound();

        policy.Name = request.Name;
        policy.Description = request.Description;
        policy.Enabled = request.Enabled;
        policy.Priority = request.Priority;
        policy.Action = request.Action;
        policy.ConditionsJson = request.ConditionsJson ?? policy.ConditionsJson;
        policy.ScopeJson = request.ScopeJson ?? policy.ScopeJson;
        policy.InsightTrigger = request.InsightTrigger ?? policy.InsightTrigger;
        policy.Revision++;
        policy.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Policies", $"Actualizada política: {request.Name}", "{}", ct);
        return Ok(new { policy.Id, policy.Revision });
    }

    [HttpPost("tenants/{tenantId:guid}/policies/{policyId:guid}/reorder")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> Reorder(Guid tenantId, Guid policyId, [FromBody] ReorderRequest request, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == policyId, ct);
        if (policy is null) return NotFound();
        policy.Priority = request.Priority;
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpDelete("tenants/{tenantId:guid}/policies/{policyId:guid}")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> Delete(Guid tenantId, Guid policyId, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == policyId, ct);
        if (policy is null) return NotFound();

        _db.Policies.Remove(policy);
        await _db.SaveChangesAsync(ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Policies", $"Eliminada política: {policy.Name}", "{}", ct);
        return Ok(new { ok = true });
    }

    private void GuardTenant(Guid tenantId)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId)
        {
            throw new UnauthorizedAccessException("No tiene acceso a esta empresa.");
        }
    }

    public record UpsertPolicyRequest(
        string Name,
        string? Description,
        PolicyKind Kind,
        bool Enabled,
        int Priority,
        PolicyAction Action,
        string? ConditionsJson,
        string? ScopeJson,
        string? InsightTrigger);

    public record ReorderRequest(int Priority);
}