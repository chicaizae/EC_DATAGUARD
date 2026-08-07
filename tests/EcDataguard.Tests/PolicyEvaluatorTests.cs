using EcDataguard.Application.Services;
using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;
using Xunit;

namespace EcDataguard.Tests;

public class PolicyEvaluatorTests
{
    private static Policy NewPolicy(string name, int priority, PolicyAction action, string conditionsJson)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = name,
            Kind = PolicyKind.Data,
            Enabled = true,
            Priority = priority,
            Action = action,
            ConditionsJson = conditionsJson,
            ScopeJson = "{}",
            InsightTrigger = "Always"
        };

    private static EventRecord NewEvent(string? destinationType = null, string classifications = "")
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ExternalId = Guid.NewGuid().ToString("N"),
            Kind = EventKind.Usb,
            DestinationType = destinationType,
            DestinationDetail = "USB [F:]",
            Classifications = classifications,
            OccurredUtc = DateTime.UtcNow
        };

    [Fact]
    public void FirstMatch_ClasificacionEncontrada_Bloquea()
    {
        var pii = NewPolicy("Bloquear USB PII", 1, PolicyAction.Block,
            "{\"destinations\":[\"external_storage\"],\"classifications\":[\"PII\"]}");
        var audit = NewPolicy("Auditar USB", 2, PolicyAction.Log,
            "{\"destinations\":[\"external_storage\"]}");

        var draft = NewEvent(destinationType: "external_storage");
        var matched = PolicyEvaluator.FirstMatch(new[] { pii, audit }, draft, new[] { "PII" });

        Assert.NotNull(matched);
        Assert.Equal(PolicyAction.Block, matched.Action);
    }

    [Fact]
    public void FirstMatch_SinClasificacion_AplicaAuditoria()
    {
        var pii = NewPolicy("Bloquear USB PII", 1, PolicyAction.Block,
            "{\"classifications\":[\"PII\"]}");
        var audit = NewPolicy("Auditar USB", 2, PolicyAction.Log,
            "{\"destinations\":[\"external_storage\"]}");

        var draft = NewEvent(destinationType: "external_storage");
        var matched = PolicyEvaluator.FirstMatch(new[] { pii, audit }, draft, new[] { "Documentos" });

        Assert.NotNull(matched);
        Assert.Equal(PolicyAction.Log, matched.Action);
    }

    [Fact]
    public void FirstMatch_DestinoDistinto_NoAplica()
    {
        var auditUsb = NewPolicy("Auditar USB", 1, PolicyAction.Log,
            "{\"destinations\":[\"external_storage\"]}");

        var draft = NewEvent(destinationType: "web_upload");
        var matched = PolicyEvaluator.FirstMatch(new[] { auditUsb }, draft, Array.Empty<string>());

        Assert.Null(matched);
    }

    [Fact]
    public void FirstMatch_JsonInvalido_SeTomaComoCoincidencia()
    {
        var policy = NewPolicy("Siempre aplica", 1, PolicyAction.Log, "{no es json");
        var draft = NewEvent(destinationType: null);

        var matched = PolicyEvaluator.FirstMatch(new[] { policy }, draft, Array.Empty<string>());

        Assert.NotNull(matched);
    }
}