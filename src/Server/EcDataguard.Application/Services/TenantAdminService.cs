using EcDataguard.Domain.Enums;
using EcDataguard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface ITenantAdminService
{
    Task<Tenant> CreateAsync(string codigo, string nombre, TenantPlan plan, CancellationToken ct);
    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct);
    Task<bool> ExistsCodeAsync(string codigo, CancellationToken ct);
    Task<Device> RegisterDeviceAsync(Guid tenantId, string hostname, OsType os, CancellationToken ct);
}

public sealed class TenantAdminService : ITenantAdminService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public TenantAdminService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Tenant> CreateAsync(string codigo, string nombre, TenantPlan plan, CancellationToken ct)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Codigo = codigo.Trim().ToUpperInvariant(),
            Nombre = nombre,
            Plan = plan,
            CreadoUtc = _clock.UtcNow
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);
        return tenant;
    }

    public async Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct)
        => await _db.Tenants.OrderBy(t => t.Nombre).ToListAsync(ct);

    public async Task<bool> ExistsCodeAsync(string codigo, CancellationToken ct)
        => await _db.Tenants.AnyAsync(t => t.Codigo == codigo.Trim().ToUpperInvariant(), ct);

    public async Task<Device> RegisterDeviceAsync(Guid tenantId, string hostname, OsType os, CancellationToken ct)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = hostname,
            Os = os,
            AgentVersion = "0.0.0",
            ProtectionState = ProtectionState.Unknown,
            CreatedUtc = _clock.UtcNow
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
        return device;
    }
}