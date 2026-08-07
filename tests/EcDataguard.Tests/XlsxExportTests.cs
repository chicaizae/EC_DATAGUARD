using System.IO.Compression;
using System.Text;
using EcDataguard.Web.Models;
using EcDataguard.Web.Services;
using Xunit;

namespace EcDataguard.Tests;

public class XlsxExportTests
{
    private static readonly EventDto Evento = new(
        Id: Guid.NewGuid(),
        ExternalId: "ext-1",
        Kind: "file",
        OccurredUtc: DateTimeOffset.UtcNow,
        UserName: "jperez",
        ProcessName: null,
        Operation: "copy",
        FilePath: "informe.xlsx",
        DestinationType: "usb",
        DestinationDetail: null,
        FileSizeBytes: 0,
        FileHash: null,
        Classifications: null,
        DbEngine: null,
        DbHost: null,
        DbPort: null,
        Detail: null,
        Blocked: true,
        PolicyAction: "Block",
        AppliedPolicyId: null);

    [Fact]
    public void Events_GeneraZipConContenidoOOXML()
    {
        var bytes = XlsxExport.Events(new[] { Evento });

        Assert.NotEmpty(bytes);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToArray();

        Assert.Contains("[Content_Types].xml", names);
        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/worksheets/sheet1.xml", names);

        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        Assert.Contains("inlineStr", sheet);
        Assert.Contains("jperez", sheet);
        Assert.Contains("informe.xlsx", sheet);
        Assert.Contains("Block", sheet);
    }

    [Fact]
    public void DownloadHref_UsaDataUriBase64DeExcel()
    {
        var bytes = XlsxExport.Events(new[] { Evento });
        var href = XlsxExport.DownloadHref(bytes);

        Assert.StartsWith("data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,", href);
        var decoded = Convert.FromBase64String(href.Split(',')[1]);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void Insights_Y_AdminTrail_GeneranContenido()
    {
        var insights = XlsxExport.Insights(new[]
        {
            new InsightDto(Guid.NewGuid(), "High", "Open", "Correo con datos", 3, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        });
        var trail = XlsxExport.AdminTrail(new[]
        {
            new AdminTrailDto(Guid.NewGuid(), null, "admin", "Tenants", "Creo empresa", DateTimeOffset.UtcNow)
        });

        Assert.Contains("Correo con datos", ReadSheet(insights));
        Assert.Contains("admin", ReadSheet(trail));
    }

    private static string ReadSheet(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return Read(zip, "xl/worksheets/sheet1.xml");
    }

    private static string Read(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name)!;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
