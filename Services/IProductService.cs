using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface IProductService
{
    Task<PaginatedResult<ProductResponse>> GetPaginatedAsync(int pageNumber, int pageSize);
    Task<ProductResponse?> GetByIdAsync(string id);
    Task<PaginatedResult<ProductResponse>> GetByCategoryAsync(string categoria, int pageNumber, int pageSize);
    Task<List<ProductResponse>> GetByStoreIdAsync(string storeId);
    Task<PaginatedResult<ProductResponse>> GetByStoreIdPaginatedAsync(string storeId, int pageNumber, int pageSize);
    Task<List<ProductResponse>> GetActiveProductsAsync();
    Task<ProductResponse> CreateAsync(CreateProductRequest request);
    Task<ProductResponse?> UpdateAsync(string id, UpdateProductRequest request);
    Task<bool> DeleteAsync(string id);

    // Nuevos métodos para gestión de imágenes
    Task<ProductResponse?> AddImageAsync(string id, string imageUrl);
    Task<ProductResponse?> RemoveImageAsync(string id, string imageUrl);
    Task<ProductResponse?> ReorderImagesAsync(string id, List<string> images);
}