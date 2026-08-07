using System.Net;
using System.Net.Mail;
using EcDataguard.Application.Services;

namespace EcDataguard.Infrastructure.Integrations;

public class SmtpOptions
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "sentinels@ecodataguard.local";
    public string FromName { get; set; } = "EC DATAGUARD";
    public bool UseSsl { get; set; }
    /// <summary>Si se define, todos los reportes se redirigen aquí (útil en pruebas).</summary>
    public string? RecipientsOverride { get; set; }
}

public sealed class SmtpReportMailer : IReportMailer
{
    private readonly SmtpOptions _options;

    public SmtpReportMailer(SmtpOptions options)
    {
        _options = options;
    }

    public bool Enabled => _options.Enabled;

    public Task<string?> SendAsync(string subject, string recipients, byte[] content, string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipients))
        {
            return Task.FromResult<string?>("No hay destinatarios configurados.");
        }

        var to = !string.IsNullOrWhiteSpace(_options.RecipientsOverride)
            ? _options.RecipientsOverride
            : recipients;

        using var message = new MailMessage();
        message.From = new MailAddress(_options.From, _options.FromName);
        message.Subject = subject;
        message.Body = "Reporte adjunto generado por EC DATAGUARD.";
        foreach (var recipient in to.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.To.Add(recipient);
        }

        using var attachment = new MemoryStream(content);
        var att = new Attachment(attachment, filename, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        message.Attachments.Add(att);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            client.Credentials = new NetworkCredential(_options.User, _options.Password);
        }

        try
        {
            client.Send(message);
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>($"Error de correo: {ex.Message}");
        }
    }
}