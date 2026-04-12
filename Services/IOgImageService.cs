namespace ApiMercadoComunidad.Services;

public record ProductOgMeta(string Title, string Description);

public interface IOgImageService
{
    Task<byte[]> GenerateProductOgImageAsync(string productId);
    Task<ProductOgMeta> GetProductMetaAsync(string productId);
}
