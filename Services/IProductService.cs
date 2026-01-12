using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public interface IProductService
{
    Task<List<Products>> GetAllAsync();
    Task<PaginatedResult<Products>> GetPaginatedAsync(int pageNumber, int pageSize);
    Task<PaginatedResult<Products>> GetByCategoriaAsync(string categoria, int pageNumber, int pageSize);
    Task<Products?> GetByIdAsync(string id);
    Task<Products> CreateAsync(Products publicacion);
    Task UpdateAsync(string id, Products publicacion);
    Task DeleteAsync(string id);
}