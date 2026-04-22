using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models.DTOs;
using Microsoft.Extensions.Options;

namespace ApiMercadoComunidad.Services;

public class PaykuService : IPaykuService
{
    private readonly PaykuSettings _settings;
    private readonly HttpClient _http;
    private readonly ILogger<PaykuService> _logger;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public PaykuService(IOptions<PaykuSettings> settings, HttpClient http, ILogger<PaykuService> logger)
    {
        _settings = settings.Value;
        _http = http;
        _logger = logger;
    }

    // Sign: HMAC-SHA256(privateToken, path&sortedParams)
    private string BuildSignature(string path, IDictionary<string, string> bodyParams)
    {
        var sortedParams = bodyParams
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}")
            .ToList();

        var message = sortedParams.Count > 0
            ? path + "&" + string.Join("&", sortedParams)
            : path;

        var keyBytes = Encoding.UTF8.GetBytes(_settings.PrivateToken);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var hash = HMACSHA256.HashData(keyBytes, msgBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var bodyParams = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ValueKind == JsonValueKind.String
                    ? kv.Value.GetString()!
                    : kv.Value.ToString());

        // Path con trailing slash requerido por Payku
        var normalizedPath = "/" + path.TrimStart('/').TrimEnd('/') + "/";
        var signature = BuildSignature(normalizedPath, bodyParams);
        var fullUrl = _settings.BaseUrl.TrimEnd('/') + normalizedPath;

        _logger.LogInformation("Payku → {Method} {Url}", method, fullUrl);

        var req = new HttpRequestMessage(method, fullUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Payku: Authorization: Bearer {public_token} + Sign: {hmac}
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.PublicToken}");
        req.Headers.TryAddWithoutValidation("Sign", signature);
        return req;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage req)
    {
        var resp = await _http.SendAsync(req);
        var content = await resp.Content.ReadAsStringAsync();

        _logger.LogInformation("Payku ← {Status} | {Body}",
            (int)resp.StatusCode, content[..Math.Min(500, content.Length)]);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Payku error {(int)resp.StatusCode}: {content[..Math.Min(300, content.Length)]}");

        if (content.TrimStart().StartsWith('<'))
            throw new InvalidOperationException(
                $"Payku devolvió HTML. Verificar URL/credenciales: {content[..Math.Min(200, content.Length)]}");

        // Payku puede devolver 200 con status:failed o message_error sin status
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("message_error", out var errProp) &&
            !string.IsNullOrWhiteSpace(errProp.GetString()))
            throw new InvalidOperationException($"Payku: {errProp.GetString()}");

        if (doc.RootElement.TryGetProperty("status", out var statusEl) &&
            statusEl.GetString() == "failed")
            throw new InvalidOperationException($"Payku: {content[..Math.Min(300, content.Length)]}");

        return JsonSerializer.Deserialize<T>(content, _json)
            ?? throw new InvalidOperationException($"Payku respuesta vacía: {content}");
    }

    public async Task<PaykuCreateClientResponse> CreateClientAsync(PaykuCreateClientRequest request)
        => await SendAsync<PaykuCreateClientResponse>(BuildRequest(HttpMethod.Post, "/api/suclient", request));

    public async Task<PaykuCreatePlanResponse> CreatePlanAsync(PaykuCreatePlanRequest request)
        => await SendAsync<PaykuCreatePlanResponse>(BuildRequest(HttpMethod.Post, "/api/suplan", request));

    public async Task<PaykuCreateSubscriptionResponse> CreateSubscriptionAsync(PaykuCreateSubscriptionRequest request)
        => await SendAsync<PaykuCreateSubscriptionResponse>(BuildRequest(HttpMethod.Post, "/api/sususcription", request));
}
