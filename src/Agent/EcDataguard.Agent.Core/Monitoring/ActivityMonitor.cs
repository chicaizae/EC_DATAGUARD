using System.Collections.Concurrent;
using EcDataguard.Contracts.Agent;

namespace EcDataguard.Agent.Monitoring;

/// <summary>
/// Agrega los monitores de actividad DLP (archivos, portapapeles, USB) en una
/// cola única. El Worker drena los eventos en cada ciclo de heartbeat.
/// </summary>
public sealed class ActivityMonitor : IDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(2);

    private readonly ConcurrentQueue<EventReport> _queue = new();
    private readonly ClipboardMonitor _clipboard;
    private readonly UsbMonitor _usb;
    private readonly FileSystemMonitor _files;
    private CancellationTokenSource? _cts;
    private int _tickCount;

    public ActivityMonitor()
    {
        _clipboard = new ClipboardMonitor(_queue);
        _usb = new UsbMonitor(_queue);
        _files = new FileSystemMonitor(_queue);
    }

    public bool Enabled { get; private set; }

    public void Start(AgentConfig config)
    {
        if (Enabled) return;
        Enabled = true;

        _files.Start(config.MonitoredFolders ?? Enumerable.Empty<string>());

        var pollers = new List<string>(2);
        if (config.MonitorClipboard && _clipboard.IsSupported) pollers.Add("clipboard");
        if (config.MonitorUsb) pollers.Add("usb");

        if (pollers.Count == 0 && (config.MonitoredFolders is null || config.MonitoredFolders.Count == 0))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => RunPollersAsync(pollers, token), CancellationToken.None);
    }

    public IReadOnlyList<EventReport> Drain()
    {
        var events = new List<EventReport>(_queue.Count);
        while (_queue.TryDequeue(out var item))
        {
            events.Add(item);
        }
        return events;
    }

    public void Dispose()
    {
        Enabled = false;
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        _files.Dispose();
    }

    private async Task RunPollersAsync(IReadOnlyList<string> pollers, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Tick, ct).ConfigureAwait(false);
                _tickCount++;
                if (pollers.Contains("clipboard"))
                {
                    _clipboard.Poll();
                }
                if (pollers.Contains("usb") && _tickCount % 5 == 0)
                {
                    _usb.Poll();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // El monitoreo nunca debe tumbar el agente.
            }
        }
    }
}