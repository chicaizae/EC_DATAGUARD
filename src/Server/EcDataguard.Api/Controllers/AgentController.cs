using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EcDataguard.Application.Abstractions;
using EcDataguard.Application.Services;
using EcDataguard.Domain.Enums;
using EcDataguard.Contracts.Agent;

namespace EcDataguard.Api.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private const string AgentVersion = "1.0.0";

    private readonly IDeviceAgentService _devices;
    private readonly IEventIngestionService _events;

    public AgentController(IDeviceAgentService devices, IEventIngestionService events)
    {
        _devices = devices;
        _events = events;
    }

    [AllowAnonymous]
    [HttpGet("trust-pack")]
    public IActionResult TrustPack() => Ok(new
    {
        product = "EcDataguard Agent",
        version = AgentVersion,
        signer = "Ecoilpet S.A.",
        serviceName = "EcDataguardAgentSvc",
        xdrCompatible = true
    });

    [Authorize(Policy = AuthClaims.AgentPolicy)]
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request, CancellationToken ct)
    {
        if (await ResolveActorAsync(ct) is not (var tenantId, var deviceId)) return Unauthorized();

        var input = new HeartbeatInput(
            deviceId,
            request.Hostname,
            MapOs(request.Os),
            request.OsVersion,
            request.AgentVersion,
            MapProtection(request.ProtectionState),
            string.Join("|", request.ProtectionDetails),
            request.ConfigRevision,
            request.UserName,
            GetCallerIp(),
            request.Databases
                .Select(d => (MapDb(d.Engine), d.Host, d.Port, d.Instance, d.Reachable))
                .ToList());

        await _devices.UpsertDeviceFromHeartbeatAsync(tenantId, input, ct);

        var pending = await _devices.GetPendingCommandsAsync(tenantId, deviceId, ct);
        return Ok(new HeartbeatResponse
        {
            ServerTimeUtc = DateTime.UtcNow,
Commands = pending
                .Select(c => new EcDataguard.Contracts.Agent.AgentCommand
                {
                    CommandId = c.Id.ToString("N"),
                    Type = (EcDataguard.Contracts.Common.AgentCommandType)(int)c.Kind,
                    PayloadJson = c.PayloadJson,
                    IssuedUtc = c.IssuedUtc
                })
                .ToList()
        });
    }

    [Authorize(Policy = AuthClaims.AgentPolicy)]
    [HttpPost("events")]
    public async Task<IActionResult> Events([FromBody] EventBatchRequest request, CancellationToken ct)
    {
        if (await ResolveActorAsync(ct) is not (var tenantId, var deviceId)) return Unauthorized();

        var inputs = request.Events.Select(e => new EventInput(
            e.EventId,
            MapKind(e.Kind),
            e.OccurredUtc,
            e.Actor?.UserName,
            e.Actor?.ProcessName,
            e.Operation,
            e.FilePath,
            e.DestinationType,
            e.DestinationDetail,
            e.FileSizeBytes,
            e.FileHashSha256,
            e.ContentScan?.Classifications ?? new List<string>() as IReadOnlyList<string>,
            e.DbArtifact != null ? MapDb(e.DbArtifact.Engine) : null,
            e.DbArtifact?.Host,
            e.DbArtifact?.Port,
            e.Detail)).ToList();

        var (accepted, rejected) = await _events.IngestAsync(tenantId, deviceId, inputs, ct);

        return Ok(new EventBatchResponse
        {
            Accepted = accepted,
            Rejected = rejected,
            NextUploadAllowedUtc = DateTime.UtcNow.AddMinutes(1)
        });
    }

    [Authorize(Policy = AuthClaims.AgentPolicy)]
    [HttpPost("commands/{commandId}/ack")]
    public async Task<IActionResult> Ack(string commandId, [FromBody] AgentCommandAck ack, CancellationToken ct)
    {
        if (await ResolveActorAsync(ct) is not (var tenantId, var deviceId)) return Unauthorized();

        var command = await _devices.AckCommandAsync(tenantId, deviceId, commandId, MapAck(ack.Status), ack.Detail, ct);
        return command is null ? NotFound() : Ok(new { ok = true });
    }

    private async Task<(Guid TenantId, Guid DeviceId)?> ResolveActorAsync(CancellationToken ct)
    {
        var tenantId = User.GetTenantId() ?? Guid.Empty;
        var deviceId = Guid.TryParse(User.GetClaim(AuthClaims.Sub), out var id) ? id : Guid.Empty;
        if (tenantId == Guid.Empty || deviceId == Guid.Empty)
        {
            return null;
        }

        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        return await _devices.IsDeviceTokenActiveAsync(tenantId, deviceId, token, ct)
            ? (tenantId, deviceId)
            : null;
    }

    private string? GetCallerIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private static OsType MapOs(Contracts.Common.OsFamily os)
        => os switch
        {
            Contracts.Common.OsFamily.Windows => OsType.Windows,
            Contracts.Common.OsFamily.Linux => OsType.Linux,
            Contracts.Common.OsFamily.MacOs => OsType.MacOs,
            _ => OsType.Unknown
        };

    private static ProtectionState MapProtection(Contracts.Common.ProtectionState s)
        => s switch
        {
            Contracts.Common.ProtectionState.Protected => ProtectionState.Protected,
            Contracts.Common.ProtectionState.Degraded => ProtectionState.Degraded,
            Contracts.Common.ProtectionState.Unprotected => ProtectionState.Unprotected,
            _ => ProtectionState.Unknown
        };

    private static EventKind MapKind(Contracts.Common.EventKind k)
        => k switch
        {
            Contracts.Common.EventKind.FileOp => EventKind.FileOp,
            Contracts.Common.EventKind.Usb => EventKind.Usb,
            Contracts.Common.EventKind.Web => EventKind.Web,
            Contracts.Common.EventKind.App => EventKind.App,
            Contracts.Common.EventKind.DbFound => EventKind.DbFound,
            Contracts.Common.EventKind.ConfigError => EventKind.ConfigError,
            _ => EventKind.AgentEvent
        };

    private static DbEngine MapDb(Contracts.Common.DbEngine e)
        => e switch
        {
            Contracts.Common.DbEngine.MsSql => DbEngine.MsSql,
            Contracts.Common.DbEngine.PostgreSql => DbEngine.PostgreSql,
            Contracts.Common.DbEngine.MySql => DbEngine.MySql,
            Contracts.Common.DbEngine.MariaDb => DbEngine.MariaDb,
            Contracts.Common.DbEngine.Oracle => DbEngine.Oracle,
            Contracts.Common.DbEngine.MongoDb => DbEngine.MongoDb,
            Contracts.Common.DbEngine.Redis => DbEngine.Redis,
            Contracts.Common.DbEngine.Elasticsearch => DbEngine.Elasticsearch,
            _ => DbEngine.Unknown
        };

    private static CommandState MapAck(Contracts.Common.AgentCommandStatus s)
        => s switch
        {
            Contracts.Common.AgentCommandStatus.Succeeded => CommandState.Succeeded,
            Contracts.Common.AgentCommandStatus.Failed => CommandState.Failed,
            Contracts.Common.AgentCommandStatus.Skipped => CommandState.Skipped,
            Contracts.Common.AgentCommandStatus.Expired => CommandState.Expired,
            _ => CommandState.Pending
        };
}