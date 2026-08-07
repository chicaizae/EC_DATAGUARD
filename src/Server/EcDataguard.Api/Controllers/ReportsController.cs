using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EcDataguard.Application.Services;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/console/tenants/{tenantId:guid}/reports")]
[Authorize(Policy = AuthClaims.ConsolePolicy)]
public class ReportsController : BaseConsoleController
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        return Ok(await _reports.ListAsync(tenantId, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid tenantId, [FromBody] NewReportRequest request, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        var report = await _reports.CreateAsync(tenantId, request, CurrentActorName(), ct);
        return Ok(report);
    }

    [HttpPut("{id:guid}/enabled")]
    public async Task<IActionResult> SetEnabled(Guid tenantId, Guid id, [FromBody] SetEnabledRequest body, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        return await _reports.SetEnabledAsync(tenantId, id, body.Enabled, CurrentActorName(), ct)
            ? Ok(new { ok = true })
            : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid tenantId, Guid id, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        return await _reports.DeleteAsync(tenantId, id, CurrentActorName(), ct)
            ? Ok(new { ok = true })
            : NotFound();
    }

    public record SetEnabledRequest(bool Enabled);
}