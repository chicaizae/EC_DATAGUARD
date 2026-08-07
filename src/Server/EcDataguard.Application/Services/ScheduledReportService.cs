using Microsoft.EntityFrameworkCore;
using EcDataguard.Application.Abstractions;
using EcDataguard.Contracts.Common;
using EcDataguard.Domain.Entities;

namespace EcDataguard.Application.Services;

public record ScheduledReportDescriptor(
    Guid Id,
    string Title,
    string ReportType,
    int HourOfDayUtc,
    string RecipientsCsv,
    bool Enabled,
    DateTime? LastRunUtc,
    DateTime? NextRunUtc,
    int TotalSent);

public record NewReportRequest(string Title, string ReportType, int HourOfDayUtc, string RecipientsCsv);

/// <summary>
/// Envío de correo usado por el planificador de reportes.
/// Devuelve null en éxito o el mensaje de error.
/// </summary>
public interface IReportMailer
{
    bool Enabled { get; }
    Task<string?> SendAsync(string subject, string recipients, byte[] content, string filename, CancellationToken ct);
}

public interface IReportService
{
    Task<IReadOnlyList<ScheduledReportDescriptor>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<ScheduledReportDescriptor> CreateAsync(Guid tenantId, NewReportRequest request, string actor, CancellationToken ct);
    Task<bool> SetEnabledAsync(Guid tenantId, Guid id, bool enabled, string actor, CancellationToken ct);
    Task<bool> DeleteAsync(Guid tenantId, Guid id, string actor, CancellationToken ct);
    Task<int> RunDueAsync(CancellationToken ct);
}

public sealed class ScheduledReportService : IReportService
{
    private static readonly string[] ValidTypes = { "eventos", "insights" };

    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IReportMailer _mailer;
    private readonly IAdminTrailService _adminTrail;

    public ScheduledReportService(IAppDbContext db, IClock clock, IReportMailer mailer, IAdminTrailService adminTrail)
    {
        _db = db;
        _clock = clock;
        _mailer = mailer;
        _adminTrail = adminTrail;
    }

    public Task<IReadOnlyList<ScheduledReportDescriptor>> ListAsync(Guid tenantId, CancellationToken ct)
        => _db.ScheduledReports
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Title)
            .Select(r => new ScheduledReportDescriptor(
                r.Id, r.Title, r.ReportType, r.HourOfDayUtc, r.RecipientsCsv,
                r.Enabled, r.LastRunUtc, r.NextRunUtc, r.TotalSent))
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ScheduledReportDescriptor>)t.Result, ct);

    public async Task<ScheduledReportDescriptor> CreateAsync(Guid tenantId, NewReportRequest request, string actor, CancellationToken ct)
    {
        var report = new ScheduledReport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title?.Trim() ?? string.Empty,
            ReportType = ValidTypes.Contains(request.ReportType?.Trim(), StringComparer.OrdinalIgnoreCase) ? request.ReportType.Trim() : "eventos",
            HourOfDayUtc = Math.Clamp(request.HourOfDayUtc, 0, 23),
            RecipientsCsv = request.RecipientsCsv ?? string.Empty,
            Enabled = true,
            NextRunUtc = NextOccurrence(_clock.UtcNow, Math.Clamp(request.HourOfDayUtc, 0, 23))
        };
        _db.ScheduledReports.Add(report);
        await _db.SaveChangesAsync(ct);

        await _adminTrail.RecordAsync(tenantId, null, actor, "Reportes",
            $"Creó el reporte programado '{report.Title}' (tipo {report.ReportType})", "{}", ct);

        return new ScheduledReportDescriptor(report.Id, report.Title, report.ReportType,
            report.HourOfDayUtc, report.RecipientsCsv, report.Enabled, report.LastRunUtc, report.NextRunUtc, report.TotalSent);
    }

    public async Task<bool> SetEnabledAsync(Guid tenantId, Guid id, bool enabled, string actor, CancellationToken ct)
    {
        var report = await _db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        if (report is null) return false;

        report.Enabled = enabled;
        await _db.SaveChangesAsync(ct);

        await _adminTrail.RecordAsync(tenantId, null, actor, "Report",
            $"{(enabled ? "Habilitó" : "Deshabilitó")} el reporte programado '{report.Title}'", "{}", ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, string actor, CancellationToken ct)
    {
        var report = await _db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        if (report is null) return false;

        _db.ScheduledReports.Remove(report);
        await _db.SaveChangesAsync(ct);

        await _adminTrail.RecordAsync(tenantId, null, actor, "Scheduled",
            $"Eliminó el reporte programado '{report.Title}'", "{}", ct);
        return true;
    }

    public async Task<int> RunDueAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var due = await _db.ScheduledReports
            .Where(r => r.Enabled && r.NextRunUtc != null && r.NextRunUtc <= now)
            .OrderBy(r => r.NextRunUtc)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        var sent = 0;
        foreach (var report in due)
        {
            if (!_mailer.Enabled)
            {
                report.NextRunUtc = NextOccurrence(_clock.UtcNow, report.HourOfDayUtc);
                await _db.SaveChangesAsync(ct);
                continue;
            }

            var since = report.LastRunUtc ?? now.AddDays(-1);
            var (content, filename) = await BuildFileAsync(report, since, ct);
            var error = await _mailer.SendAsync(ReportSubject(report, now), report.RecipientsCsv, content, filename, ct);

            report.LastRunUtc = now;
            report.NextRunUtc = NextOccurrence(_clock.UtcNow, report.HourOfDayUtc);
            report.TotalSent += error == null ? 1 : 0;
            await _db.SaveChangesAsync(ct);

            if (error == null) sent++;
        }
        return sent;
    }

    private static string ReportSubject(ScheduledReport report, DateTime now)
        => $"[EC DATAGUARD] Reporte {report.ReportType} · {now:dd/MM/yyyy} · {report.Title}";

    private async Task<(byte[] Content, string FileName)> BuildFileAsync(ScheduledReport report, DateTime since, CancellationToken ct)
    {
        if (report.ReportType == "insights")
        {
            var rows = await _db.Insights
                .Where(i => i.TenantId == report.TenantId && i.CreatedUtc >= since)
                .OrderByDescending(i => i.CreatedUtc)
                .Take(500)
                .ToListAsync(ct);

            var content = ReportXlsxBuilder.Build("Insights",
                new[] { "Fecha", "Severidad", "Estado", "Razon", "Eventos", "Cierre" },
                rows.Select(i => new string?[]
                {
                    i.CreatedUtc.ToString("O"),
                    i.Severity.ToString(),
                    i.Status.ToString(),
                    i.Reason,
                    i.RelatedEventCount.ToString(),
                    i.ClosureReason
                }));

            return (content, $"insights-{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }

        var events = await _db.Events
            .Where(e => e.TenantId == report.TenantId && e.IngestedUtc >= since)
            .OrderByDescending(e => e.IngestedUtc)
            .Take(500)
            .ToListAsync(ct);

        var eventRows = ReportXlsxBuilder.Build("Eventos",
            new[] { "Fecha", "Tipo", "Usuario", "Operacion", "Archivo", "Destino", "Bloqueado", "Accion" },
            events.Select(e => new string?[]
            {
                e.OccurredUtc.ToString("O"),
                e.Kind.ToString(),
                e.UserName,
                e.Operation,
                e.FilePath,
                e.DestinationType,
                e.Blocked ? "Si" : "No",
                e.AppliedAction?.ToString()
            }));

        return (eventRows, $"eventos-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private static DateTime NextOccurrence(DateTime now, int hourOfDayUtc)
    {
        var today = new DateTime(now.Year, now.Month, now.Day, hourOfDayUtc, 0, 0, DateTimeKind.Utc);
        return today <= now ? today.AddDays(1) : today;
    }
}