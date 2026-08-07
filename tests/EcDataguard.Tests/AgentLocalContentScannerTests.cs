using EcDataguard.Agent.Monitoring;
using Xunit;

namespace EcDataguard.Tests;

public class AgentLocalContentScannerTests
{
    [Theory]
    [InlineData("Contacto: juan.perez@ecoilpet.com.ec")]
    [InlineData("La cédula es 1712345678 y no la comparta")]
    [InlineData("La contraseña provisional es ecDg@2026")]
    public void Scan_TextoConPII_DevuelvePII(string text)
    {
        var hits = LocalContentScanner.Scan(text);

        Assert.Contains("PII", hits);
    }

    [Theory]
    [InlineData("Tarjeta 4111 1111 1111 1111 expira en 2027")]
    [InlineData("Haga la transferencia a la cuenta bancaria 0123456789012345")]
    public void Scan_TextoFinanciero_DevuelveFinanciero(string text)
    {
        var hits = LocalContentScanner.Scan(text);

        Assert.Contains("Financiero", hits);
    }

    [Fact]
    public void Scan_TextoInocuo_NoDevuelveHits()
    {
        Assert.Empty(LocalContentScanner.Scan("Pedido de compra nro 10 con 3 unidades."));
    }

    [Fact]
    public void Scan_NuloOBlanco_NoDevuelveHits()
    {
        Assert.Empty(LocalContentScanner.Scan(null));
        Assert.Empty(LocalContentScanner.Scan("   "));
    }

    [Theory]
    [InlineData("contrato.pdf", "pdf")]
    [InlineData("clientes.xlsx", "spreadsheet")]
    [InlineData("backup.sql", "database")]
    [InlineData("codigo.cs", "source")]
    [InlineData("lote.zip", "archive")]
    public void DetectFileType_PorExtension_DetectaTipo(string path, string expected)
    {
        Assert.Equal(expected, LocalContentScanner.DetectFileType(path));
    }
}