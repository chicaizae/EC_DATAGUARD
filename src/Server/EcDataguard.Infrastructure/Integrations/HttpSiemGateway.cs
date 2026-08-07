using System.Net.Http.Headers;
using EcDataguard.Application.Abstractions;

namespace EcDataguard.Infrastructure.Integrations;

public sealed class SiemOptions
{
    public bool Enabled { get; set; }
    public string? WebhookUrl { get; set; }
    public string? CustomHeaderName { get; set; }
    public string? CustomHeaderValue { get; set; }
}

public sealed class HttpSiemGateway : ISiemGateway
{
    private readonly SiemOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpSiemGateway(SiemOptions options, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public bool Enabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.WebhookUrl);

    public async Task<DeliveryResult> DeliverAsync(Guid? tenantId, string payloadJson, CancellationToken ct)
    {
        if (!Enabled) return new DeliveryResult(true, null, "siem-disabled");

        var client = _httpClientFactory.CreateClient("siem");
        var request = new HttpRequestMessage(HttpMethod.Post, _options.WebhookUrl)
        {
            Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.CustomHeaderName))
        {
            request.Headers.TryAddWithoutValidation(_options.CustomHeaderName, _options.CustomHeaderValue ?? string.Empty);
        }

        try
        {
            var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return new DeliveryResult(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? null : body, "webhook");
        }
        catch (Exception ex)
        {
            return new DeliveryResult(false, ex.Message, "webhook");
        }
    }
}