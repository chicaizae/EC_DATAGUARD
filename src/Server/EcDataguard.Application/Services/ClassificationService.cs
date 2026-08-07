using System.Text.RegularExpressions;
using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;

namespace EcDataguard.Application.Services;

public interface IClassificationService
{
    IReadOnlyList<string> MatchContent(string content, IEnumerable<Classification> classifications);
    IReadOnlyList<string> MatchClassificationsScan(string? classifications, string? destinationType);
    string? DetectFileType(string? filePath, byte[]? header = null);
}

public sealed class ClassificationService : IClassificationService
{
    private static readonly Regex EmailRegex = new(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    private static readonly Regex EcuadorIdRegex = new(@"\b\d{10}(?:\d{3})?\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    private static readonly Regex PhoneRegex = new(@"(?<!\d)(?:\+?593|0)?(?:9\d{8}|[2-7]\d{7})(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    private static readonly Regex CardCandidateRegex = new(@"(?<!\d)(?:\d[ -]?){13,19}(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    private static readonly string[] PiiKeywords =
    {
        "cedula", "cédula", "pasaporte", "dni", "ruc", "fecha de nacimiento", "direccion", "dirección"
    };

    private static readonly string[] FinancialKeywords =
    {
        "cuenta bancaria", "iban", "swift", "tarjeta de credito", "tarjeta de crédito", "cvv", "saldo", "transferencia"
    };

    public IReadOnlyList<string> MatchContent(string content, IEnumerable<Classification> classifications)
    {
        var hits = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddBuiltInHits(content, hits, seen);

        foreach (var classification in classifications.Where(c => c.Enabled))
        {
            foreach (var rule in classification.Rules)
            {
                if (rule.Type != RuleType.Content) continue;

                var found = rule.IsRegex
                    ? SafeRegexIsMatch(content, rule.Pattern)
                    : content.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);

                if (found)
                {
                    AddHit(classification.Name, hits, seen);
                    break;
                }
            }
        }
        return hits;
    }

    public IReadOnlyList<string> MatchClassificationsScan(string? classifications, string? destinationType)
    {
        var hits = new List<string>();
        if (!string.IsNullOrWhiteSpace(classifications))
        {
            var parts = classifications.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts) hits.Add(part);
        }
        if (!string.IsNullOrWhiteSpace(destinationType))
        {
            hits.Add($"dest:{destinationType}");
        }
        return hits;
    }

    public string? DetectFileType(string? filePath, byte[]? header = null)
    {
        var extension = Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant();

        if (header is { Length: >= 4 })
        {
            if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46) return "pdf";
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return "image";
            if (header[0] == 0xFF && header[1] == 0xD8) return "image";
            if (header[0] == 0x4D && header[1] == 0x5A) return "executable";
            if (header[0] == 0x50 && header[1] == 0x4B)
            {
                return extension switch
                {
                    ".docx" => "document",
                    ".xlsx" => "spreadsheet",
                    ".pptx" => "presentation",
                    _ => "archive"
                };
            }
        }

        return extension switch
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
    }

    private static void AddBuiltInHits(string content, List<string> hits, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        if (EmailRegex.IsMatch(content)
            || EcuadorIdRegex.IsMatch(content)
            || PhoneRegex.IsMatch(content)
            || PiiKeywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            AddHit("PII", hits, seen);
        }

        if (ContainsValidCard(content)
            || FinancialKeywords.Any(k => content.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            AddHit("Financiero", hits, seen);
        }
    }

    private static bool ContainsValidCard(string content)
    {
        foreach (Match match in CardCandidateRegex.Matches(content))
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

    private static bool SafeRegexIsMatch(string content, string pattern)
    {
        try
        {
            return Regex.IsMatch(content, pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(200));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static void AddHit(string name, List<string> hits, HashSet<string> seen)
    {
        if (seen.Add(name)) hits.Add(name);
    }
}
