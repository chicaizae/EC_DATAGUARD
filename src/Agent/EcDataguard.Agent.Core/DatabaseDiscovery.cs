using System.Diagnostics;
using System.Net.Sockets;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Agent;

public class DatabaseDiscovery
{
    private static readonly (string Refname, DbEngine Engine)[] KnownProcesses =
    {
        ("sqlservr", DbEngine.MsSql),
        ("postgres", DbEngine.PostgreSql),
        ("mysqld", DbEngine.MySql),
        ("mariadbd", DbEngine.MariaDb),
        ("oracle", DbEngine.Oracle),
        ("mongod", DbEngine.MongoDb),
        ("redis-server", DbEngine.Redis),
        ("elasticsearch", DbEngine.Elasticsearch)
    };

    private static readonly (int Port, DbEngine Engine)[] KnownPorts =
    {
        (1433, DbEngine.MsSql),
        (5432, DbEngine.PostgreSql),
        (3306, DbEngine.MySql),
        (1521, DbEngine.Oracle),
        (27017, DbEngine.MongoDb),
        (6379, DbEngine.Redis),
        (9200, DbEngine.Elasticsearch)
    };

    public IReadOnlyList<DbArtifactInfo> Scan(string? ip = null)
    {
        ip ??= "127.0.0.1";
        var result = new List<DbArtifactInfo>();
        var processNames = GetRunningProcessNames();

        foreach (var (process, engine) in KnownProcesses)
        {
            if (processNames.Contains(process, StringComparer.OrdinalIgnoreCase))
            {
                var knownPort = KnownPorts.FirstOrDefault(k => k.Engine == engine).Port;
                result.Add(new DbArtifactInfo
                {
                    Engine = engine,
                    Host = ip,
                    Port = knownPort,
                    Instance = process,
                    Reachable = true
                });
            }
        }

        foreach (var (port, engine) in KnownPorts)
        {
            if (IsPortReachable(ip, port, 500))
            {
                if (!result.Any(r => r.Engine == engine && r.Port == port))
                {
                    result.Add(new DbArtifactInfo { Engine = engine, Host = ip, Port = port, Instance = null, Reachable = true });
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<string> GetRunningProcessNames()
    {
        var names = new List<string>();
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                names.Add(process.ProcessName ?? string.Empty);
            }
        }
        catch
        {
            // Puede requerir permisos; en ese caso se degrada al escaneo de puertos.
        }
        return names;
    }

    private static bool IsPortReachable(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            return task.Wait(timeoutMs) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}