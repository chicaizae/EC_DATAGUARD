using System;
using System.Collections.Generic;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Contracts.Policies;

public class PolicyScope
{
    public List<string> Teams { get; set; } = new();
    public List<string> Users { get; set; } = new();
}

public class PolicyConditions
{
    public List<string> Destinations { get; set; } = new();
    public List<string> Classifications { get; set; } = new();
}

public class PolicyDescriptor
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public PolicyScope Scope { get; set; } = new();
    public PolicyConditions Conditions { get; set; } = new();
    public PolicyEngineAction Action { get; set; }
    public string InsightTrigger { get; set; } = "Default";
}

public class PolicySet
{
    public int PolicySetVersion { get; set; }
    public List<PolicyDescriptor> Policies { get; set; } = new();
}