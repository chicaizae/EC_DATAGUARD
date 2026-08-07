using System.Text.RegularExpressions;

namespace EcDataguard.Agent.Monitoring;

/// <summary>
/// Escáner local ligero que detecta datos sensibles (PII / financiero)
/// en texto capturado (portapapeles, nombres de archivo) sin subir el
/// contenido bruto al servidor. Etiquetas alineadas con la clasificación
/// del servidor ("PII", "Financiero").
/// </summary>
public static class LocalContentScanner
{
    private static readonly Regex EmailRegex = NewRegex(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");

    private static readonly Regex EcuadorIdRegex = NewRegex(
        @"(?<!\d)\d{10}(?!\d)");

    private static readonly Regex PhoneRegex = NewRegex(
        @"(?<!\d)(?:\+?\d[\s\-\.]?){7,15}\d(?!\d)");

    private static readonly Regex CardCandidateRegex = NewRegex(
        @"(?<!\d)(?:\d[ \-]?){13,19}(?!\d)");

    private static readonly string[] PiiKeywords = { "clave", "password", "contraseña", "cédula", "cedula", "seguro social", "número de cuenta" };
    private static readonly string[] FinancialKeywords = { "tarjeta", "visa", "mastercard", "amex", "iban", "swift", "transferencia", "cuenta bancaria" };

    public static IReadOnlyList<string> Scan(string? text)
    {
        var hits = new List<string>(2);
        if (string.IsNullOrWhiteSpace(text))
        {
            return hits;
        }

        if (EmailRegex.IsMatch(text)
            || EcuadorIdRegex.IsMatch(text)
            || PhoneRegex.IsMatch(text)
            || PiiKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            hits.Add("PII");
        }

        if (ContainsValidCard(text)
            || FinancialKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            hits.Add("Financiero");
        }

        return hits;
    }

    public static string? DetectFileType(string? filePath)
        => Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant() switch
        {
            ".pdf" => "pdf",
            ".doc" or ".docx" or ".odt" or ".rtf" or ".txt" => "document",
            ".xls" or ".xlsx" or ".ods" or ".csv" => "spreadsheet",
            ".ppt" or ".pptx" or ".odp" => "presentation",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" => "image",
            ".zip" or ".rar" or ".7z" or ".gz" or ".tar" => "archive",
            ".exe" or ".dll" or ".msi" or ".bat" or ".ps1" or ".sh" => "executable",
            ".db" or ".sqlite" or ".sqlite3" or ".bak" or ".sql" => "database",
            ".cs" or ".js" or ".ts" or ".py" or ".java" or ".go" or ".php" => "source",
            _ => null
        };

    private static bool ContainsValidCard(string text)
    {
        foreach (Match match in CardCandidateRegex.Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits))
            {
                return true;
            }
        }
        return false;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private static Regex NewRegex(string pattern)
        => new(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(200));
}
