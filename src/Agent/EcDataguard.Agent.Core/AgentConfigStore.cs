using System.Text.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using EcDataguard.Contracts.Common;
using EcDataguard.Contracts.Policies;

namespace EcDataguard.Agent;

public class AgentConfig
{
    public Guid DeviceId { get; set; }
    public string ServerUrl { get; set; } = "http://localhost:8080/api";
    public string DeviceToken { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int PolicySetVersion { get; set; }
    public bool CollectDatabases { get; set; } = true;
    public bool ScanNetwork { get; set; }
    public bool SophosTest { get; set; }
    public bool MonitorClipboard { get; set; } = true;
    public bool MonitorUsb { get; set; } = true;
    public List<string> MonitoredFolders { get; set; } = new();
}

public class AgentConfigStore
{
    private readonly string _path;
    private AgentConfig? _config;

    public AgentConfigStore(string BaseDirectory)
    {
        _path = Path.Combine(BaseDirectory, "agent-config.json");
    }

    public string FilePath => _path;

    public AgentConfig Load()
    {
        if (_config != null) return _config;
        if (File.Exists(_path))
        {
            _config = JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(_path))
                ?? new AgentConfig();
            return _config;
        }
        _config = new AgentConfig
        {
            DeviceId = Guid.NewGuid()
        };
        Save(new AgentConfig { DeviceId = _config.DeviceId });
        return new AgentConfig { DeviceId = _config.DeviceId };
    }

    public void Save(AgentConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        _config = config;
    }

    public void SavePolicies(string policiesJson)
    {
        var path = Path.Combine(Path.GetDirectoryName(_path)!, "policies.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, policiesJson);
    }

    public PolicySet? LoadPolicies()
    {
        var path = Path.Combine(Path.GetDirectoryName(_path)!, "policies.json");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<PolicySet>(File.ReadAllText(path), SetOptions);
        }
        catch
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions SetOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public bool ApplyPolicySet(PolicySet set)
    {
        var current = LoadPolicies();
        var next = JsonSerializer.Serialize(set, SetOptions);
        if (current is not null && JsonSerializer.Serialize(current, SetOptions) == next)
        {
            return false;
        }
        SavePolicies(next);
        return true;
    }
}

public static class AgentEnvironment
{
    public static string DataDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EcDatagard", "Agent");
            }
            return "/etc/ecdataguard/agent";
        }
    }

    public static OsFamily CurrentOs
        => OperatingSystem.IsWindows() ? OsFamily.Windows
        : OperatingSystem.IsLinux() ? OsFamily.Linux
        : OperatingSystem.IsMacOS() ? OsFamily.MacOs
        : OsFamily.Unknown;

    public static string Hostname => Environment.MachineName;
    public static string AgentVersion => "1.0.0";
    public static string CurrentUser
        => Environment.UserName;

    public static long UptimeSeconds
        => (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;

    public static string GetScriptForInstall()
    {
        if (OperatingSystem.IsWindows())
        {
            return "sc create EcDataguardAgentSvc binPath= \"<ruta-absoluta>\\EcDataguardAgent.exe --service\" start= auto";
        }
        return "[Unit]\nDescription=EC DATAGUARD Agent\nAfter=network.target\n\n[Service]\nType=simple\nExecStart=<ruta>/EcDataguardAgent --service\nRestart=always\n\n[Install]\nWantedBy=multi-user.target";
    }
}