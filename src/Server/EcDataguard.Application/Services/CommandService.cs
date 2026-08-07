using System.Text.Json;
using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface ICommandService
{
    Task<AgentCommand> QueueCommandAsync(Guid tenantId, Guid deviceId, AgentCommandKind kind, object payload, CancellationToken ct);
    Task<int> QueueSetConfigAsync(Guid tenantId, Guid deviceId, int heartbeatIntervalSeconds, bool dbScan, bool scanNetwork, CancellationToken ct);
}

public sealed class CommandService : ICommandService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public CommandService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AgentCommand> QueueCommandAsync(Guid tenantId, Guid deviceId, AgentCommandKind kind, object payload, CancellationToken ct)
    {
        var command = new AgentCommand
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            Kind = kind,
            PayloadJson = JsonSerializer.Serialize(payload),
            State = CommandState.Pending,
            IssuedUtc = _clock.UtcNow
        };
        _db.AgentCommands.Add(command);
        await _db.SaveChangesAsync(ct);
        return command;
    }

    public async Task<int> QueueSetConfigAsync(Guid tenantId, Guid deviceId, int heartbeatIntervalSeconds, bool dbScan, bool scanNetwork, CancellationToken ct)
    {
        var command = new AgentCommand
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            Kind = AgentCommandKind.SetConfig,
            PayloadJson = JsonSerializer.Serialize(new
            {
                heartbeatIntervalSeconds,
                collection = new { databases = dbScan, events = true, networkScan = scanNetwork }
            }),
            State = CommandState.Pending,
            IssuedUtc = _clock.UtcNow
        };
        _db.AgentCommands.Add(command);
        return await _db.SaveChangesAsync(ct);
    }
}