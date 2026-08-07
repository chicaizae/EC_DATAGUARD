using EcDataguard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Application.Services;

public interface IAdminTrailService
{
    Task RecordAsync(Guid? tenantId, Guid? actorUserId, string actorName, string section, string activity, string contentJson, CancellationToken ct);
}

public sealed class AdminTrailService : IAdminTrailService
{
    private readonly IAppDbContext _db;
    private readonly ISiemGateway _siem;
    private readonly IClock _clock;

    public AdminTrailService(IAppDbContext db, ISiemGateway siem, IClock clock)
    {
        _db = db;
        _siem = siem;
        _clock = clock;
    }

    public async Task RecordAsync(Guid? tenantId, Guid? actorUserId, string actorName, string section, string activity, string contentJson, CancellationToken ct)
    {
        var entry = new AdminAction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActorName = actorName ?? "system",
            Section = section ?? string.Empty,
            Activity = activity ?? string.Empty,
            ContentJson = contentJson ?? "{}",
            OccurredUtc = _clock.UtcNow
        };
        _db.AdminActions.Add(entry);
        await _db.SaveChangesAsync(ct);

        if (!_siem.Enabled) return;
        var payload = SiemPayloadBuilder.AdminAction(entry);
        var result = await _siem.DeliverAsync(tenantId, payload, ct);
        _db.SiemDeliveryLogs.Add(new SiemDeliveryLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Target = result.Target,
            PayloadJson = payload,
            SentUtc = _clock.UtcNow,
            Success = result.Success,
            Error = result.Error
        });
        await _db.SaveChangesAsync(ct);
    }
}
