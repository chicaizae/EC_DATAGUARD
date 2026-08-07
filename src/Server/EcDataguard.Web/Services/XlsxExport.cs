using System.IO.Compression;
using System.Text;
using EcDataguard.Web.Models;

namespace EcDataguard.Web.Services;

public static class XlsxExport
{
    public static byte[] Events(IEnumerable<EventDto> events)
        => Build(
            new[] { "Fecha", "Tipo", "Usuario", "Operacion", "Archivo", "Destino", "Bloqueado", "Accion" },
            events.Select(e => new[]
            {
                e.OccurredUtc.ToString("O"),
                e.Kind,
                e.UserName,
                e.Operation,
                e.FilePath,
                e.DestinationType,
                e.Blocked ? "Si" : "No",
                e.PolicyAction
            }));

    public static byte[] Insights(IEnumerable<InsightDto> insights)
        => Build(
            new[] { "Fecha", "Severidad", "Estado", "Razon", "Eventos relacionados", "Ultima actividad" },
            insights.Select(i => new[]
            {
                i.CreatedUtc?.ToString("O"),
                i.Severity,
                i.Status,
                i.Reason,
                i.RelatedEventCount.ToString(),
                i.LastActivityUtc?.ToString("O")
            }));

    public static byte[] AdminTrail(IEnumerable<AdminTrailDto> entries)
        => Build(
            new[] { "Fecha", "Actor", "Seccion", "Actividad", "Empresa" },
            entries.Select(a => new[]
            {
                a.OccurredUtc.ToString("O"),
                a.ActorName,
                a.Section,
                a.Activity,
                a.TenantId?.ToString()
            }));

    public static string DownloadHref(byte[] xlsx)
        => "data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,"
           + Convert.ToBase64String(xlsx);

    private static byte[] Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                + "</Types>");

            Write(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");

            Write(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                + "<sheets><sheet name=\"Datos\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
                + "</workbook>");

            Write(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
                + "</Relationships>");

            Write(zip, "xl/worksheets/sheet1.xml", SheetXml(headers, rows));
        }
        return ms.ToArray();
    }

    private static string SheetXml(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        var all = new[] { headers }.Concat(rows.Select(r => r.Select(v => v ?? string.Empty).ToArray()));
        var rowIndex = 1;
        foreach (var row in all)
        {
            sb.Append("<row r=\"").Append(rowIndex).Append("\">");
            for (var c = 0; c < row.Count; c++)
            {
                var cellRef = ColumnName(c) + rowIndex;
                sb.Append("<c r=\"").Append(cellRef).Append("\" t=\"inlineStr\"><is><t>")
                  .Append(XmlEscape(row[c]))
                  .Append("</t></is></c>");
            }
            sb.Append("</row>");
            rowIndex++;
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        index++;
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }
        return name;
    }

    private static string XmlEscape(string value)
        => value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

    private static void Write(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
