using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public interface IPublicacionService
{
    Task<List<Publication>> GetAllAsync();
    Task<PaginatedResult<Publication>> GetPaginatedAsync(int pageNumber, int pageSize);
    Task<PaginatedResult<Publication>> GetByCategoriaAsync(string categoria, int pageNumber, int pageSize);
    Task<Publication?> GetByIdAsync(string id);
    Task<Publication> CreateAsync(Publication publicacion);
    Task UpdateAsync(string id, Publication publicacion);
    Task DeleteAsync(string id);
}