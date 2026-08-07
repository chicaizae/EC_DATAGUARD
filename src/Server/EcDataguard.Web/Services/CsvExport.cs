using System.Text;
using EcDataguard.Web.Models;

namespace EcDataguard.Web.Services;

public static class CsvExport
{
    public static string Events(IEnumerable<EventDto> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Tipo,Usuario,Operacion,Archivo,Destino,Bloqueado,Accion");
        foreach (var e in events)
        {
            Row(sb,
                e.OccurredUtc.ToString("O"),
                e.Kind,
                e.UserName,
                e.Operation,
                e.FilePath,
                e.DestinationType,
                e.Blocked ? "Si" : "No",
                e.PolicyAction);
        }
        return sb.ToString();
    }

    public static string Insights(IEnumerable<InsightDto> insights)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Severidad,Estado,Razon,Eventos relacionados,Ultima actividad");
        foreach (var i in insights)
        {
            Row(sb,
                i.CreatedUtc?.ToString("O"),
                i.Severity,
                i.Status,
                i.Reason,
                i.RelatedEventCount.ToString(),
                i.LastActivityUtc?.ToString("O"));
        }
        return sb.ToString();
    }

    public static string AdminTrail(IEnumerable<AdminTrailDto> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Actor,Seccion,Actividad,Empresa");
        foreach (var a in entries)
        {
            Row(sb,
                a.OccurredUtc.ToString("O"),
                a.ActorName,
                a.Section,
                a.Activity,
                a.TenantId?.ToString());
        }
        return sb.ToString();
    }

    public static string DownloadHref(string csv)
        => "data:text/csv;charset=utf-8," + Uri.EscapeDataString(csv);

    private static void Row(StringBuilder sb, params string?[] values)
    {
        sb.AppendLine(string.Join(',', values.Select(Escape)));
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }
        return value;
    }
}
