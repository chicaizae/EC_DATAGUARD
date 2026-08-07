using Xunit;
using EcDataguard.Agent.Monitoring;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;
using EcDataguard.Contracts.Policies;

namespace EcDataguard.Tests;

public class LocalPolicyEngineTests
{
    private static PolicyDescriptor Block(string name, int priority, string? clasif = "Financiero", string? dest = "clipboard")
        => new()
        {
            Id = Guid.NewGuid().ToString("D"),
            Name = name,
            Enabled = true,
            Priority = priority,
            Conditions = new PolicyConditions
            {
                Destinations = dest is null ? new() : new List<string> { dest },
                Classifications = clasif is null ? new() : new List<string> { clasif }
            },
            Action = PolicyEngineAction.Block,
            InsightTrigger = "Default"
        };

    private static EventReport ClipboardEvent(params string[] classifications)
        => new()
        {
            EventId = Guid.NewGuid().ToString("N"),
            Kind = EventKind.App,
            DestinationType = "clipboard",
            ContentScan = new ContentScanResult { Done = true, Classifications = classifications.ToList() }
        };

    [Fact]
    public void Bloquea_Clipboard_Con_Clasificacion()
    {
        var policies = new List<PolicyDescriptor> { Block("bloquear-tarjeta", 1) };

        var evt = ClipboardEvent("Financiero");
        var match = LocalPolicyEngine.FindFirstMatch(policies, evt);
        LocalPolicyEngine.ApplyPolicy(match, evt);

        Assert.NotNull(match);
        Assert.True(evt.Blocked);
        Assert.Equal(PolicyEngineAction.Block, evt.AppliedAction);
        Assert.Equal(match!.Id, evt.PolicyId);
        Assert.Contains("Bloqueada", evt.Detail);
    }

    [Fact]
    public void No_Coincide_Sin_Clasificacion_Solicitada()
    {
        var policies = new List<PolicyDescriptor> { Block("1", 1) };

        var evt = ClipboardEvent("PII");
        var match = LocalPolicyEngine.FindFirstMatch(policies, evt);

        Assert.Null(match);
        LocalPolicyEngine.ApplyPolicy(null, evt);
        Assert.False(evt.Blocked);
        Assert.Null(evt.AppliedAction);
        Assert.Null(evt.PolicyId);
    }

    [Fact]
    public void Coincide_Por_Solo_Destino()
    {
        var policies = new List<PolicyDescriptor> { Block("usb", 3, clasif: null, dest: "usb") };
        var evt = new EventReport
        {
            EventId = "ev",
            Kind = EventKind.Usb,
            DestinationType = "usb",
            ContentScan = new ContentScanResult { Done = true }
        };

        var match = LocalPolicyEngine.FindFirstMatch(policies, evt);

        Assert.NotNull(match);
    }

    [Fact]
    public void Accion_Log_No_Bloquea()
    {
        var policy = Block("log", 2);
        policy.Action = PolicyEngineAction.Log;
        var policies = new List<PolicyDescriptor> { policy };

        var evt = ClipboardEvent("Financiero");
        var match = LocalPolicyEngine.FindFirstMatch(policies, evt);
        LocalPolicyEngine.ApplyPolicy(match, evt);

        Assert.NotNull(match);
        Assert.False(evt.Blocked);
        Assert.Equal(PolicyEngineAction.Log, evt.AppliedAction);
    }

    [Fact]
    public void Aplica_La_Primera_Por_Prioridad()
    {
        var primero = Block("primero", 1);
        var segundo = Block("segundo", 5);
        var policies = new List<PolicyDescriptor> { segundo, primero };

        var evt = ClipboardEvent("Financiero");
        var match = LocalPolicyEngine.FindFirstMatch(policies, evt);

        Assert.Equal(primero.Id, match!.Id);
    }

    [Fact]
    public void Ignora_Politicas_Deshabilitadas()
    {
        var disabled = Block("off", 1);
        disabled.Enabled = false;

        var evt = ClipboardEvent("Financiero");
        var match = LocalPolicyEngine.FindFirstMatch(new List<PolicyDescriptor> { disabled }, evt);

        Assert.Null(match);
    }
}