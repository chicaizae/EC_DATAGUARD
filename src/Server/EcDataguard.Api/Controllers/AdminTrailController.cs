using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EcDataguard.Application.Services;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/console")]
[Authorize(Policy = AuthClaims.ConsolePolicy)]
public class AdminTrailController : BaseConsoleController
{
    private readonly IConsoleQueryService _queries;

    public AdminTrailController(IConsoleQueryService queries) => _queries = queries;

    [HttpGet("admin-trail")]
    public async Task<IActionResult> List([FromQuery] int limit = 200, CancellationToken ct = default)
    {
        var scope = EffectiveTenantScopeOrNull();
        var entries = await _queries.GetAdminTrailAsync(scope, Math.Clamp(limit, 1, 1000), ct);
        return Ok(entries.Select(a => new
        {
            a.Id,
            a.TenantId,
            a.ActorName,
            a.Section,
            a.Activity,
            a.OccurredUtc,
            a.ContentJson
        }));
    }
}