using ApiMercadoComunidad.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;

namespace ApiMercadoComunidad.Services;

public class TurnstileService : ITurnstileService
{
    private readonly HttpClient _httpClient;
    private readonly TurnstileSettings _settings;
    private readonly ILogger<TurnstileService> _logger;

    public TurnstileService(HttpClient httpClient, IOptions<TurnstileSettings> settings, ILogger<TurnstileService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> ValidateTokenAsync(string? token, string? remoteIp = null)
    {
        if (!_settings.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            _logger.LogWarning("Turnstile enabled but SecretKey is empty.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Turnstile token is empty.");
            return false;
        }

        try
        {
            var payload = new Dictionary<string, string>
            {
                ["secret"] = _settings.SecretKey,
                ["response"] = token.Trim()
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
                payload["remoteip"] = remoteIp;

            using var content = new FormUrlEncodedContent(payload);
            using var response = await _httpClient.PostAsync(_settings.VerifyUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verify returned status code {StatusCode}", (int)response.StatusCode);
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var verifyResponse = await JsonSerializer.DeserializeAsync<TurnstileVerifyResponse>(stream);
            if (verifyResponse?.Success == true)
                return true;

            var errors = verifyResponse?.ErrorCodes is { Count: > 0 }
                ? string.Join(",", verifyResponse.ErrorCodes)
                : "none";
            _logger.LogWarning("Turnstile verification failed. Error codes: {ErrorCodes}", errors);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Turnstile token");
            return false;
        }
    }

    private sealed class TurnstileVerifyResponse
    {
        public bool Success { get; set; }
        [JsonPropertyName("error-codes")]
        public List<string>? ErrorCodes { get; set; }
    }
}
