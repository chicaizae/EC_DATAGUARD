using EcDataguard.Application.Services;
using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Xunit;

namespace EcDataguard.Tests;

public class ClassificationServiceTests
{
    private static Classification NewClassification(string name, bool enabled = true, params ClassificationRule[] rules)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = name,
            Enabled = enabled,
            Rules = rules.ToList()
        };

    private static ClassificationRule Rule(RuleType type, string pattern, bool isRegex)
        => new()
        {
            Id = Guid.NewGuid(),
            ClassificationId = Guid.NewGuid(),
            Type = type,
            Pattern = pattern,
            IsRegex = isRegex
        };

    [Fact]
    public void MatchContent_RegexDeCedula_DetectaPII()
    {
        var pii = NewClassification("PII", true,
            Rule(RuleType.Content, @"\b\d{13}\b", true));

        var hits = new ClassificationService().MatchContent("El cliente 1712345678901 envió sus datos.", new[] { pii });

        Assert.Contains("PII", hits);
    }

    [Fact]
    public void MatchContent_TarjetaDeCredito_DetectaFinanciero()
    {
        var fin = NewClassification("Financiero", true,
            Rule(RuleType.Content, @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14})\b", true));

        var hits = new ClassificationService().MatchContent("Visa 4111111111111111 ok", new[] { fin });

        Assert.Contains("Financiero", hits);
    }

    [Fact]
    public void MatchContent_SinCoincidencia_DevuelveVacio()
    {
        var pii = NewClassification("PII", true,
            Rule(RuleType.Content, @"\b\d{13}\b", true));

        var hits = new ClassificationService().MatchContent("sin datos personales", new[] { pii });

        Assert.Empty(hits);
    }

    [Fact]
    public void MatchContent_ClasificacionDeshabilitada_SeOmite()
    {
        var pii = NewClassification("PII", false,
            Rule(RuleType.Content, "marcador-interno", false));

        var hits = new ClassificationService().MatchContent("marcador-interno", new[] { pii });

        Assert.Empty(hits);
    }

    [Fact]
    public void MatchContent_TextoLibreNoSensible_Coincide()
    {
        var doc = NewClassification("Documentos", true,
            Rule(RuleType.Content, "confidencial", false));

        var hits = new ClassificationService().MatchContent("CONFIDENCIAL — informe 2026", new[] { doc });

        Assert.Contains("Documentos", hits);
    }

    [Fact]
    public void MatchContent_Email_DetectaPiiSinReglasTenant()
    {
        var hits = new ClassificationService().MatchContent("Contacto: auditor@example.com", Array.Empty<Classification>());

        Assert.Contains("PII", hits);
    }

    [Fact]
    public void MatchContent_TarjetaValidaLuhn_DetectaFinancieroSinReglasTenant()
    {
        var hits = new ClassificationService().MatchContent("Pago con 4111 1111 1111 1111", Array.Empty<Classification>());

        Assert.Contains("Financiero", hits);
    }

    [Fact]
    public void MatchContent_TarjetaInvalida_NoDetectaFinanciero()
    {
        var hits = new ClassificationService().MatchContent("Numero 4111 1111 1111 1112", Array.Empty<Classification>());

        Assert.DoesNotContain("Financiero", hits);
    }

    [Fact]
    public void MatchContent_ReglaTenantYBase_NoDuplicaEtiqueta()
    {
        var pii = NewClassification("PII", true,
            Rule(RuleType.Content, "auditor@example.com", false));

        var hits = new ClassificationService().MatchContent("auditor@example.com", new[] { pii });

        Assert.Single(hits.Where(h => h == "PII"));
    }

    [Fact]
    public void MatchClassificationsScan_CombinaClasificacionesYDestino()
    {
        var service = new ClassificationService();

        var hits = service.MatchClassificationsScan("PII|Financiero", "external_storage");

        Assert.Equal(new[] { "PII", "Financiero", "dest:external_storage" }, hits);
    }

    [Fact]
    public void MatchClassificationsScan_Vacio_DevuelveVacio()
    {
        var hits = new ClassificationService().MatchClassificationsScan(null, null);
        Assert.Empty(hits);
    }

    [Theory]
    [InlineData("contrato.pdf", "pdf")]
    [InlineData("clientes.xlsx", "spreadsheet")]
    [InlineData("backup.sql", "database")]
    [InlineData("codigo.cs", "source")]
    [InlineData("archivo.zip", "archive")]
    public void DetectFileType_PorExtension_DetectaTipo(string path, string expected)
    {
        Assert.Equal(expected, new ClassificationService().DetectFileType(path));
    }

    [Fact]
    public void DetectFileType_PorFirmaPdf_IgnoraExtensionIncorrecta()
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

        Assert.Equal("pdf", new ClassificationService().DetectFileType("archivo.bin", header));
    }

    [Fact]
    public void DetectFileType_ZipOffice_UsaExtensionParaRefinar()
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        Assert.Equal("document", new ClassificationService().DetectFileType("informe.docx", header));
        Assert.Equal("spreadsheet", new ClassificationService().DetectFileType("reporte.xlsx", header));
        Assert.Equal("archive", new ClassificationService().DetectFileType("lote.zip", header));
    }
}
