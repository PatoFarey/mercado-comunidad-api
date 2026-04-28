namespace ApiMercadoComunidad.Services;

public record ProductOgMeta(string Title, string Description);
public record StoreOgMeta(string Title, string Description);

public interface IOgImageService
{
    Task<byte[]> GenerateProductOgImageAsync(string productId);
    Task<ProductOgMeta> GetProductMetaAsync(string productId);
    Task<byte[]> GenerateStoreOgImageAsync(string slugOrId);
    Task<StoreOgMeta> GetStoreMetaAsync(string slugOrId);
}
