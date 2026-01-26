namespace ApiMercadoComunidad.Services;

public interface IProductSynchronizeService
{
    Task<bool> SynchronizeProductAsync(string productId);
    Task<int> SynchronizeAllProductsAsync();
    Task<int> SynchronizeProductsByStoreAsync(string storeId);
}