using System.Collections.Concurrent;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Agent.Monitoring;

/// <summary>
/// Detecta conexión/desconexión de unidades removibles (USB, tarjetas SD)
/// mediante sondeo de DriveInfo y encola eventos DLP de tipo Usb.
/// </summary>
public sealed class UsbMonitor
{
    private readonly ConcurrentQueue<EventReport> _queue;
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _knownOrder = new();

    public UsbMonitor(ConcurrentQueue<EventReport> queue)
    {
        _queue = queue;
    }

    public void Poll()
    {
        List<string>? current = null;
        try
        {
            current = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable)
                .Select(d => d.Name)
                .ToList();
        }
        catch (Exception)
        {
            return;
        }

        foreach (var drive in current)
        {
            if (_known.Add(drive))
            {
                _knownOrder.Add(drive);
                _queue.Enqueue(Build("usb_attach", drive));
            }
        }

        for (var i = _knownOrder.Count - 1; i >= 0; i--)
        {
            if (current.Contains(_knownOrder[i])) continue;
            var removed = _knownOrder[i];
            _knownOrder.RemoveAt(i);
            _known.Remove(removed);
            _queue.Enqueue(Build("usb_detach", removed));
        }
    }

    private static EventReport Build(string operation, string drive)
    {
        return new EventReport
        {
            EventId = Guid.NewGuid().ToString("N"),
            OccurredUtc = DateTime.UtcNow,
            Kind = EventKind.Usb,
            Actor = new ActorInfo { UserName = AgentEnvironment.CurrentUser, ProcessName = "ecdataguard-agent" },
            Operation = operation,
            DestinationType = "usb",
            DestinationDetail = drive,
            Detail = operation == "usb_attach" ? $"Dispositivo de almacenamiento removible conectado ({drive})." : $"Dispositivo de almacenamiento removible desconectado ({drive})."
        };
    }
}
