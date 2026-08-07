namespace EcDataguard.Domain.Entities;

/// <summary>
/// Suscripción a un reporte periódico (XLSX) enviado por correo por tenant.
/// El host planificador genera y envía el reporte cuando vence la siguiente fecha.
/// </summary>
public class ScheduledReport
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>eventos | insights</summary>
    public string ReportType { get; set; } = "eventos";

    /// <summary>Hora UTC del día en la que se envía.</summary>
    public int HourOfDayUtc { get; set; } = 6;

    /// <summary>Destinatarios separados por ';'.</summary>
    public string RecipientsCsv { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public int TotalSent { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}