using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public interface ICommunityProductService
{
    Task<List<CommunityProduct>> GetByCommunityIdAsync(string communityId);
    Task<PaginatedResult<CommunityProduct>> GetByCommunityIdPaginatedAsync(string communityId, int pageNumber, int pageSize);
    Task<List<CommunityProduct>> GetByCategoriaAsync(string communityId, string categoria);
    Task<CommunityProduct?> GetByIdAsync(string id);
    Task<CommunityProduct> CreateAsync(CommunityProduct communityProduct);
    Task UpdateAsync(string id, CommunityProduct communityProduct);
    Task DeleteAsync(string id);
}