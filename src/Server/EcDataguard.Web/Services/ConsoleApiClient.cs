using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcDataguard.Web.Services;

public class ConsoleSession
{
    public string? Token { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ScopeTenantId { get; set; }
    public bool IsSuperAdmin => Role == "SuperAdmin";
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public void Clear()
    {
        Token = null;
        Email = null;
        Role = null;
        TenantId = null;
        ScopeTenantId = null;
    }
}

public class ConsoleApiClient
{
    private readonly HttpClient _http;
    private readonly ConsoleSession _session;

    public ConsoleApiClient(HttpClient http, ConsoleSession session)
    {
        _http = http;
        _session = session;
    }

    public ConsoleSession Session => _session;

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        _session.Token = root.GetProperty("token").GetString();
        _session.Email = root.GetProperty("email").GetString();
        _session.Role = root.GetProperty("role").GetString();
        _session.TenantId = root.TryGetProperty("tenantId", out var t) && t.ValueKind == JsonValueKind.String
            ? Guid.Parse(t.GetString()!)
            : null;
        _session.ScopeTenantId = root.TryGetProperty("scopeTenantId", out var s) && s.ValueKind == JsonValueKind.String
            ? Guid.Parse(s.GetString()!)
            : null;
        return true;
    }

    public void SetScope(Guid? tenantId)
    {
        _session.ScopeTenantId = tenantId;
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        AttachAuth(request);
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(ct), JsonDefaults.Options);
    }

    public async Task<TOut?> PostAsync<TIn, TOut>(string path, TIn body, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonDefaults.Options), Encoding.UTF8, "application/json")
        };
        AttachAuth(request);
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<TOut>(await response.Content.ReadAsStringAsync(ct), JsonDefaults.Options);
    }

    private void AttachAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_session.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        }
    }
}

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}