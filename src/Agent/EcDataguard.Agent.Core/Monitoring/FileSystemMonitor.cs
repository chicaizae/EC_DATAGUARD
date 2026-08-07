using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Agent.Monitoring;

/// <summary>
/// Observa carpetas configuradas con FileSystemWatcher y encola eventos FileOp
/// (creación, escritura, borrado, renombrado) con tipo de archivo y clasificación local.
/// </summary>
public sealed class FileSystemMonitor : IDisposable
{
    private const int MaxHashBytes = 512 * 1024;
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(3);

    private readonly ConcurrentQueue<EventReport> _queue;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _recent = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemMonitor(ConcurrentQueue<EventReport> queue)
    {
        _queue = queue;
    }

    public void Start(IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;

            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.DirectoryName
            };
            watcher.Created += (_, e) => Push("create", e.FullPath);
            watcher.Changed += (_, e) => Push("write", e.FullPath);
            watcher.Deleted += (_, e) => Push("delete", e.FullPath);
            watcher.Renamed += (_, e) => Push("rename", e.FullPath);
            watcher.Error += (_, e) => OnError(e.GetException());

            try
            {
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception)
            {
                watcher.Dispose();
            }
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            try { watcher.EnableRaisingEvents = false; } catch { /* best effort */ }
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    public void Dispose() => Stop();

    private void OnError(Exception? ex)
    {
        System.Diagnostics.Debug.WriteLine($"FileSystemWatcher error: {ex?.Message}");
    }

    private void Push(string operation, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var now = DateTime.UtcNow;
        if (!_recent.TryAdd(path, now))
        {
            if (now - _recent[path] < Debounce) return;
            _recent[path] = now;
        }

        var classifications = new List<string>();
        var fileType = LocalContentScanner.DetectFileType(path);
        if (fileType != null)
        {
            classifications.Add($"file:{fileType}");
        }

        _queue.Enqueue(new EventReport
        {
            EventId = Guid.NewGuid().ToString("N"),
            OccurredUtc = now,
            Kind = EventKind.FileOp,
            Actor = new ActorInfo { UserName = AgentEnvironment.CurrentUser, ProcessName = "ecdataguard-agent" },
            Operation = operation,
            FilePath = path,
            DestinationType = "file",
            FileSizeBytes = TrySize(path),
            FileHashSha256 = TryHash(path),
            ContentScan = new ContentScanResult { Done = true, Classifications = classifications }
        });
    }

    private static long TrySize(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    private static string? TryHash(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxHashBytes) return null;

            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        catch (Exception)
        {
            return null;
        }
    }
}