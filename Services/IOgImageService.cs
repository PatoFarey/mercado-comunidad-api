namespace ApiMercadoComunidad.Services;

public interface IOgImageService
{
    Task<byte[]> GenerateProductOgImageAsync(string productId);
}
