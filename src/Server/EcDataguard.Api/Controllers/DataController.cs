using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EcDataguard.Application.Services;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/console")]
[Authorize(Policy = AuthClaims.ConsolePolicy)]
public class EventController : BaseConsoleController
{
    private readonly IConsoleQueryService _queries;
    private readonly IAdminTrailService _trail;

    public EventController(IConsoleQueryService queries, IAdminTrailService trail)
    {
        _queries = queries;
        _trail = trail;
    }

    [HttpGet("tenants/{tenantId:guid}/events")]
    public async Task<IActionResult> List(Guid tenantId, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        var events = await _queries.GetEventsAsync(tenantId, Math.Clamp(limit, 1, 500), ct);
        return Ok(events.Select(ev => new
        {
            ev.Id,
            ev.ExternalId,
            Kind = ev.Kind.ToString(),
            ev.OccurredUtc,
            ev.UserName,
            ev.ProcessName,
            ev.Operation,
            ev.FilePath,
            ev.DestinationType,
            ev.DestinationDetail,
            ev.Blocked,
            PolicyAction = ev.AppliedAction?.ToString()
        }));
    }

    [HttpGet("tenants/{tenantId:guid}/events/{eventId:guid}")]
    public async Task<IActionResult> GetEvent(Guid tenantId, Guid eventId, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        var ev = await _queries.GetEventAsync(tenantId, eventId, ct);
        if (ev is null) return NotFound();

        return Ok(new
        {
            ev.Id,
            ev.ExternalId,
            Kind = ev.Kind.ToString(),
            ev.OccurredUtc,
            ev.UserName,
            ev.ProcessName,
            ev.Operation,
            ev.FilePath,
            ev.DestinationType,
            ev.DestinationDetail,
            ev.FileSizeBytes,
            ev.FileHash,
            ev.Classifications,
            ev.DbEngine,
            ev.DbHost,
            ev.DbPort,
            ev.Detail,
            ev.Blocked,
            PolicyAction = ev.AppliedAction?.ToString(),
            ev.AppliedPolicyId
        });
    }

    [HttpGet("tenants/{tenantId:guid}/insights")]
    public async Task<IActionResult> Insights(Guid tenantId, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        var insights = await _queries.GetInsightsAsync(tenantId, ct);
        return Ok(insights.Select(i => new
        {
            i.Id,
            Severity = i.Severity.ToString(),
            Status = i.Status.ToString(),
            i.Reason,
            i.RelatedEventCount,
            i.LastActivityUtc,
            i.CreatedUtc
        }));
    }

    [HttpPost("tenants/{tenantId:guid}/insights/{insightId:guid}/close")]
    public async Task<IActionResult> CloseInsight(Guid tenantId, Guid insightId, [FromBody] CloseInsightRequest request, CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId) return Forbid();

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Closed from console" : request.Reason.Trim();
        await _queries.CloseInsightAsync(tenantId, insightId, reason, CurrentActorId(), ct);
        await _trail.RecordAsync(tenantId, CurrentActorId(), CurrentActorName(), "Insights", "Closed insight",
            $"{{\"insightId\":\"{insightId}\",\"reason\":\"{reason}\"}}", ct);
        return Ok(new { ok = true });
    }

    public record CloseInsightRequest(string? Reason);
}
