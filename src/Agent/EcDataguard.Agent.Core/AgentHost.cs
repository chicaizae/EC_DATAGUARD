using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EcDataguard.Agent;

public static class AgentHost
{
    public static async Task RunAsync(string[] args)
    {
        if (args.Length > 0 && args[0] is "install" or "uninstall")
        {
            Console.WriteLine(AgentEnvironment.GetScriptForInstall());
            return;
        }

        if (args.Any(a => a.Equals("--version", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"Agente EC DATAGUARD v{AgentEnvironment.AgentVersion}");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        var store = new AgentConfigStore(AgentEnvironment.DataDirectory);
        var config = store.Load();

        if (TryGetArg(args, "--server", out var serverUrl))
        {
            config.ServerUrl = serverUrl.TrimEnd('/');
            store.Save(config);
        }

        if (TryGetArg(args, "--token", out var token))
        {
            config.DeviceToken = token;
            store.Save(config);
        }

        if (TryGetArg(args, "--policies-file", out var policiesFile) && File.Exists(policiesFile))
        {
            store.SavePolicies(File.ReadAllText(policiesFile));
            config.PolicySetVersion = 1;
            store.Save(config);
        }

        if (TryGetArg(args, "--device-id", out var deviceId) && Guid.TryParse(deviceId, out var parsed))
        {
            config.DeviceId = parsed;
            store.Save(config);
        }

        if (TryGetArg(args, "--monitor-clipboard", out var clipboardArg) && bool.TryParse(clipboardArg, out var monitorClipboard))
        {
            config.MonitorClipboard = monitorClipboard;
            store.Save(config);
        }

        if (TryGetArg(args, "--monitor-usb", out var usbArg) && bool.TryParse(usbArg, out var monitorUsb))
        {
            config.MonitorUsb = monitorUsb;
            store.Save(config);
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--monitor-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                config.MonitoredFolders.Add(Path.GetFullPath(args[i + 1]));
            }
        }
        if (config.MonitoredFolders.Count > 0)
        {
            store.Save(config);
        }

        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton<DatabaseDiscovery>();
        builder.Services.AddSingleton<CommandExecutor>();
        builder.Services.AddSingleton<Monitoring.ActivityMonitor>();
        builder.Services.AddHttpClient<AgentClient>().ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.AddHostedService<AgentWorker>();

        var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Agent");
        logger.LogInformation("Agente EC DATAGUARD iniciado (device={Device}, server={Server})", config.DeviceId, config.ServerUrl);

        await host.RunAsync();
    }

    private static bool TryGetArg(string[] args, string name, out string value)
    {
        value = string.Empty;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                value = args[i + 1];
                return true;
            }
        }
        return false;
    }
}