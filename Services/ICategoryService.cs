using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync();
    Task<CategoryResponse?> GetByIdAsync(string id);
    Task<CategoryResponse?> GetByNameAsync(string name);
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request);
    Task<CategoryResponse?> UpdateAsync(string id, UpdateCategoryRequest request);
    Task<bool> DeleteAsync(string id);
    Task<bool> CategoryExistsAsync(string name);
}