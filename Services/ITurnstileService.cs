namespace ApiMercadoComunidad.Services;

public interface ITurnstileService
{
    Task<bool> ValidateTokenAsync(string? token, string? remoteIp = null);
}
