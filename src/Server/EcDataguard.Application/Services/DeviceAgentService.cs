using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface IDeviceAgentService
{
    Task<Device> UpsertDeviceFromHeartbeatAsync(Guid tenantId, HeartbeatInput hb, CancellationToken ct);
    Task<IReadOnlyList<AgentCommand>> GetPendingCommandsAsync(Guid tenantId, Guid deviceId, CancellationToken ct);
    Task<AgentCommand?> AckCommandAsync(Guid tenantId, Guid deviceId, string externalCommandId, CommandState state, string? detail, CancellationToken ct);
    Task<DeviceToken> StoreDeviceTokenAsync(Guid tenantId, Guid deviceId, string token, TimeSpan lifetime, CancellationToken ct);
    Task<bool> IsDeviceTokenActiveAsync(Guid tenantId, Guid deviceId, string token, CancellationToken ct);
    Task<int> RevokeDeviceTokensAsync(Guid tenantId, Guid deviceId, CancellationToken ct);
}

public sealed record HeartbeatInput(
    Guid DeviceId,
    string Hostname,
    OsType Os,
    string? OsVersion,
    string AgentVersion,
    ProtectionState ProtectionState,
    string ProtectionDetails,
    int ConfigRevision,
    string? UserName,
    string? IpAddress,
    IReadOnlyList<(DbEngine Engine, string Host, int Port, string? Instance, bool Reachable)> Databases);

public sealed class DeviceAgentService : IDeviceAgentService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public DeviceAgentService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Device> UpsertDeviceFromHeartbeatAsync(Guid tenantId, HeartbeatInput hb, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == hb.DeviceId, ct);
        if (device is null)
        {
            device = new Device
            {
                Id = hb.DeviceId,
                TenantId = tenantId,
                Hostname = hb.Hostname,
                CreatedUtc = _clock.UtcNow
            };
            _db.Devices.Add(device);
        }

        device.Hostname = hb.Hostname;
        device.Os = hb.Os;
        device.OsVersion = hb.OsVersion;
        device.AgentVersion = hb.AgentVersion;
        device.ProtectionState = hb.ProtectionState;
        device.ProtectionDetails = hb.ProtectionDetails;
        device.ConfigRevision = hb.ConfigRevision;
        device.LastUser = hb.UserName;
        device.IpAddress = hb.IpAddress;
        device.LastHeartbeatUtc = _clock.UtcNow;
        device.Deleted = false;

        if (!string.IsNullOrWhiteSpace(hb.UserName))
        {
            await UpsertEndpointUserAsync(tenantId, hb.UserName, _clock.UtcNow, ct);
        }

        foreach (var dbArtifact in hb.Databases)
        {
            var existing = await _db.DeviceDbArtifacts.FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.DeviceId == device.Id
                     && a.Engine == dbArtifact.Engine && a.Port == dbArtifact.Port && a.Host == dbArtifact.Host, ct);

            if (existing is null)
            {
                _db.DeviceDbArtifacts.Add(new DeviceDbArtifact
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DeviceId = device.Id,
                    Engine = dbArtifact.Engine,
                    Host = dbArtifact.Host,
                    Port = dbArtifact.Port,
                    Instance = dbArtifact.Instance,
                    Reachable = dbArtifact.Reachable,
                    DiscoveredUtc = _clock.UtcNow,
                    LastSeenUtc = _clock.UtcNow
                });
            }
            else
            {
                existing.Instance = dbArtifact.Instance;
                existing.Reachable = dbArtifact.Reachable;
                existing.LastSeenUtc = _clock.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return device;
    }

    private async Task UpsertEndpointUserAsync(Guid tenantId, string userName, DateTime activityUtc, CancellationToken ct)
    {
        var normalized = userName.Trim();
        if (normalized.Length == 0) return;

        var user = await _db.EndpointUsers.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UserName == normalized, ct);
        if (user is null)
        {
            user = new EndpointUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserName = normalized,
                Licensed = true,
                LastActivityUtc = activityUtc
            };
            _db.EndpointUsers.Add(user);
            return;
        }

        user.Licensed = true;
        if (user.LastActivityUtc is null || user.LastActivityUtc < activityUtc)
        {
            user.LastActivityUtc = activityUtc;
        }
    }

    public async Task<IReadOnlyList<AgentCommand>> GetPendingCommandsAsync(Guid tenantId, Guid deviceId, CancellationToken ct)
        => await _db.AgentCommands
            .Where(c => c.TenantId == tenantId && c.DeviceId == deviceId && c.State == CommandState.Pending)
            .OrderBy(c => c.IssuedUtc)
            .Take(50)
            .ToListAsync(ct);

    public async Task<AgentCommand?> AckCommandAsync(Guid tenantId, Guid deviceId, string externalCommandId, CommandState state, string? detail, CancellationToken ct)
    {
        if (!Guid.TryParse(externalCommandId, out var commandGuid)) return null;

        var command = await _db.AgentCommands
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.DeviceId == deviceId && c.Id == commandGuid, ct);

        if (command is null) return null;

        command.State = state;
        command.ResultDetail = detail;
        command.AppliedUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return command;
    }

    public async Task<DeviceToken> StoreDeviceTokenAsync(Guid tenantId, Guid deviceId, string token, TimeSpan lifetime, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == deviceId, ct);
        if (device is null)
        {
            throw new InvalidOperationException("El dispositivo no existe en esta empresa.");
        }

        var tokenEntity = new DeviceToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            TokenHash = TokenHasher.Hash(token),
            IssuedUtc = _clock.UtcNow,
            ExpiresUtc = _clock.UtcNow.Add(lifetime)
        };
        _db.DeviceTokens.Add(tokenEntity);
        await _db.SaveChangesAsync(ct);
        return tokenEntity;
    }

    public async Task<bool> IsDeviceTokenActiveAsync(Guid tenantId, Guid deviceId, string token, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var row = await _db.DeviceTokens.FirstOrDefaultAsync(t =>
            t.TenantId == tenantId && t.DeviceId == deviceId, ct);
        return row is not null && TokenState.IsActive(row, token, now);
    }

    public async Task<int> RevokeDeviceTokensAsync(Guid tenantId, Guid deviceId, CancellationToken ct)
    {
        var tokens = await _db.DeviceTokens
            .Where(t => t.TenantId == tenantId && t.DeviceId == deviceId && !t.Revoked)
            .ToListAsync(ct);

        foreach (var t in tokens)
        {
            t.Revoked = true;
        }
        await _db.SaveChangesAsync(ct);
        return tokens.Count;
    }
}
