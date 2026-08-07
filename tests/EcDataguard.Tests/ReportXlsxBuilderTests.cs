using System.IO.Compression;
using System.Text;
using EcDataguard.Application.Services;
using Xunit;

namespace EcDataguard.Tests;

public class ReportXlsxBuilderTests
{
    [Fact]
    public void Build_GeneraZipOOXMLConContenido()
    {
        var bytes = ReportXlsxBuilder.Build("Eventos",
            new[] { "Fecha", "Tipo" },
            new[] { new string?[] { "2026-08-07", "file" }, new string?[] { "2026-08-06", "usb" } });

        Assert.NotEmpty(bytes);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToArray();

        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/worksheets/sheet1.xml", names);

        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        Assert.Contains("inlineStr", sheet);
        Assert.Contains("usb", sheet);

        var workbook = Read(zip, "xl/workbook.xml");
        Assert.Contains("Eventos", workbook);
    }

    private static string Read(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name)!;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}