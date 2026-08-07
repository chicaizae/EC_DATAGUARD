using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Agent.Monitoring;

/// <summary>
/// Monitorea el portapapeles de texto (solo Windows) por sondeo con Win32.
/// Detecta cambios de texto y encola un evento App con clasificación local.
/// </summary>
public sealed class ClipboardMonitor
{
    private const uint CfUnicodeText = 13;
    private const int MaxTextLength = 4096;

    private readonly ConcurrentQueue<EventReport> _queue;
    private string _lastText = string.Empty;

    public ClipboardMonitor(ConcurrentQueue<EventReport> queue)
    {
        _queue = queue;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public void Poll()
    {
        if (!OperatingSystem.IsWindows()) return;

        string? text = null;
        try
        {
            text = ReadText();
        }
        catch (Exception)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text) || text == _lastText) return;

        if (text.Length > MaxTextLength)
        {
            text = text[..MaxTextLength];
        }

        _lastText = text;

        var classifications = LocalContentScanner.Scan(text);
        if (classifications.Count == 0) return;

        _queue.Enqueue(new EventReport
        {
            EventId = Guid.NewGuid().ToString("N"),
            OccurredUtc = DateTime.UtcNow,
            Kind = EventKind.App,
            Actor = new ActorInfo { UserName = AgentEnvironment.CurrentUser, ProcessName = "ecdataguard-agent" },
            Operation = "clipboard_copy",
            DestinationType = "clipboard",
            FileSizeBytes = text.Length,
            Detail = "Texto copiado al portapapeles con datos sensibles.",
            ContentScan = new ContentScanResult { Done = true, Classifications = classifications.ToList() }
        });
    }

    private static string? ReadText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            if (!IsClipboardFormatAvailable(CfUnicodeText)) return null;
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero) return null;

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
