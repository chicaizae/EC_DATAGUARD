using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;
using EcDataguard.Application.Services;
using EcDataguard.Domain.Enums;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/console")]
[Authorize(Policy = AuthClaims.ConsolePolicy)]
public class DevicesController : BaseConsoleController
{
    private readonly IConsoleQueryService _queries;
    private readonly ITenantAdminService _tenants;
    private readonly ITokenService _tokens;
    private readonly ICommandService _commands;
    private readonly IAdminTrailService _trail;
    private readonly IDeviceAgentService _agents;
    private readonly IAppDbContext _db;
    private readonly IConfiguration _config;

    public DevicesController(
        IConsoleQueryService queries,
        ITenantAdminService tenants,
        ITokenService tokens,
        ICommandService commands,
        IAdminTrailService trail,
        IDeviceAgentService agents,
        IAppDbContext db,
        IConfiguration config)
    {
        _queries = queries;
        _tenants = tenants;
        _tokens = tokens;
        _commands = commands;
        _trail = trail;
        _agents = agents;
        _db = db;
        _config = config;
    }

    [HttpGet("tenants/{tenantId:guid}/devices")]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var devices = await _queries.GetDevicesAsync(tenantId, ct);
        return Ok(devices.Select(d => new
        {
            d.Id,
            d.Hostname,
            Os = d.Os.ToString(),
            d.AgentVersion,
            Protection = d.ProtectionState.ToString(),
            d.LastHeartbeatUtc,
            Online = d.Online,
            d.LastUser
        }));
    }

    [HttpPost("tenants/{tenantId:guid}/devices")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> Register(Guid tenantId, [FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var device = await _tenants.RegisterDeviceAsync(tenantId, request.Hostname, request.Os, ct);

        var deviceJwt = _tokens.IssueDeviceToken(tenantId, device.Id, TimeSpan.FromDays(365));
        await _agents.StoreDeviceTokenAsync(tenantId, device.Id, deviceJwt, TimeSpan.FromDays(365), ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Devices", "Registrado dispositivo",
            $"{{\"hostname\":\"{device.Hostname}\"}}", ct);

        return Ok(new { device.Id, device.Hostname, DeviceToken = deviceJwt });
    }

    [HttpGet("tenants/{tenantId:guid}/devices/{deviceId:guid}")]
    public async Task<IActionResult> Get(Guid tenantId, Guid deviceId, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var devices = await _queries.GetDevicesAsync(tenantId, ct);
        var device = devices.FirstOrDefault(d => d.Id == deviceId);
        if (device is null) return NotFound();
        return Ok(new
        {
            device.Id,
            device.Hostname,
            Os = device.Os.ToString(),
            device.OsVersion,
            device.AgentVersion,
            Protection = device.ProtectionState.ToString(),
            device.ProtectionDetails,
            device.LastHeartbeatUtc,
            device.Online,
            device.LastUser,
            device.IpAddress,
            device.CreatedUtc
        });
    }

    [HttpPost("tenants/{tenantId:guid}/devices/{deviceId:guid}/commands")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> IssueCommand(Guid tenantId, Guid deviceId, [FromBody] IssueCommandRequest request, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var command = await _commands.QueueCommandAsync(tenantId, deviceId, request.Kind, request.Payload ?? new { }, ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Devices.Commands", $"Mandado: {request.Kind}", "{}", ct);
        return Ok(new { command.Id, command.Kind, command.State });
    }

    [HttpGet("tenants/{tenantId:guid}/databases")]
    public async Task<IActionResult> Databases(Guid tenantId, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var artifacts = await _queries.GetDbArtifactsAsync(tenantId, ct);
        return Ok(artifacts.Select(a => new
        {
            a.Id,
            a.DeviceId,
            a.Engine,
            a.Host,
            a.Port,
            a.Instance,
            a.Reachable,
            a.DiscoveredUtc
        }));
    }

    [HttpPost("tenants/{tenantId:guid}/devices/{deviceId:guid}/installer")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> GetInstaller(Guid tenantId, Guid deviceId, [FromBody] InstallerRequest request, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var body = await BuildInstallerAsync(tenantId, deviceId, request.Os, ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Devices.Installer", "Generado instalador", "{}", ct);
        return Ok(body);
    }

    [HttpPost("tenants/{tenantId:guid}/devices/{deviceId:guid}/token/revoke")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> RevokeToken(Guid tenantId, Guid deviceId, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var exists = await _db.Devices.AnyAsync(d => d.TenantId == tenantId && d.Id == deviceId, ct);
        if (!exists) return NotFound();

        var revoked = await _agents.RevokeDeviceTokensAsync(tenantId, deviceId, ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Devices.Token", "Revocado token", "{}", ct);
        return Ok(new { revoked });
    }

    [HttpPost("tenants/{tenantId:guid}/devices/{deviceId:guid}/token/reissue")]
    [Authorize(Policy = AuthClaims.SuperAdminPolicy)]
    public async Task<IActionResult> ReissueToken(Guid tenantId, Guid deviceId, [FromBody] InstallerRequest request, CancellationToken ct)
    {
        GuardTenant(tenantId);
        var body = await BuildInstallerAsync(tenantId, deviceId, request.Os, ct);

        await _trail.RecordAsync(tenantId, null, CurrentActorName(), "Devices.Token", "Reemitido token", "{}", ct);
        return Ok(body);
    }

    private async Task<object> BuildInstallerAsync(Guid tenantId, Guid deviceId, string os, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == deviceId, ct);
        if (device is null) throw new KeyNotFoundException("El dispositivo no existe");

        _ = MapInstaller(os); // valida el SO solicitado

        await _agents.RevokeDeviceTokensAsync(tenantId, deviceId, ct);

        var expiry = TimeSpan.FromDays(365);
        var token = _tokens.IssueDeviceToken(tenantId, deviceId, expiry);
        await _agents.StoreDeviceTokenAsync(tenantId, deviceId, token, expiry, ct);

        var server = (_config["Agent:ServerUrl"] ?? "http://localhost:8080/api").TrimEnd('/');
        var baseUrl = (_config["Agent:DownloadBaseUrl"] ?? server).TrimEnd('/');

        var (folder, file, command) = os.ToLowerInvariant() switch
        {
            "win7" => ("win7", "EcDataguardAgent7.exe",
                $"EcDataguardAgent7.exe --server {server} --token {token}"),
            "linux" => ("linux", "ecdataguard-agent",
                $"sudo ./ecdataguard-agent --server {server} --token {token}"),
            _ => ("win10", "EcDataguardAgent.exe",
                $"EcDataguardAgent.exe --server {server} --token {token}")
        };

        return new
        {
            downloadUrl = $"{baseUrl}/agents/{folder}/{file}",
            fileName = file,
            command,
            token
        };
    }

    private static string MapInstaller(string os)
        => os.ToLowerInvariant() switch
        {
            "win7" => "win7",
            "linux" => "linux",
            _ => "win10"
        };

    private void GuardTenant(Guid tenantId)
    {
        var scope = EffectiveTenantScopeOrNull();
        if (scope.HasValue && scope.Value != tenantId)
        {
            throw new UnauthorizedAccessException("No tiene acceso a esta empresa.");
        }
    }

    public record RegisterDeviceRequest(string Hostname, OsType Os);
    public record IssueCommandRequest(AgentCommandKind Kind, object? Payload = null);
    public record InstallerRequest(string Os);
}
