using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EcDataguard.Application.Services;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/console")]
[Authorize(Policy = AuthClaims.ConsolePolicy)]
public class DashboardController : BaseConsoleController
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var scope = EffectiveTenantScopeOrNull();
        var snapshot = await _dashboard.GetAsync(scope, ct);
        return Ok(snapshot);
    }

    [HttpGet("global")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> Global(CancellationToken ct)
    {
        var snapshot = await _dashboard.GetAsync(null, ct);
        return Ok(snapshot);
    }
}