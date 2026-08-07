using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EcDataguard.Contracts.Agent;
using EcDataguard.Contracts.Common;

namespace EcDataguard.Agent;

public class AgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _http;
    private readonly AgentConfig _config;

    public AgentClient(HttpClient http, AgentConfig config)
    {
        _http = http;
        _config = config;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }

    public async Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken ct)
    {
        return await SendAsync<HeartbeatRequest, HeartbeatResponse>("/agent/heartbeat", request, ct);
    }

    public async Task AckAsync(AgentCommandAck ack, CancellationToken ct)
    {
        await SendAsync<AgentCommandAck, object>($"/agent/commands/{ack.CommandId}/ack", ack, ct);
    }

    public async Task<EventBatchResponse> SendEventsAsync(EventBatchRequest batch, CancellationToken ct)
    {
        return await SendAsync<EventBatchRequest, EventBatchResponse>("/agent/events", batch, ct);
    }

    public async Task<JsonDocument> GetTrustPackAsync(CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.ServerUrl.TrimEnd('/')}/agent/trust-pack");
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.ServerUrl.TrimEnd('/')}{path}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.DeviceToken);

        var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Token de agente rechazado. Re-empareje el dispositivo.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Servidor {response.StatusCode}: {Truncate(content)}");
        }

        if (typeof(TResponse) == typeof(object))
        {
            return default!;
        }
        return JsonSerializer.Deserialize<TResponse>(content, JsonOptions) ?? default!;
    }

    private static string Truncate(string value, int max = 300)
        => value.Length <= max ? value : value[..max];
}