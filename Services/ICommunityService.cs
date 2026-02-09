using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public interface ICommunityService
{
    Task<List<Community>> GetAllAsync();
    Task<Community?> GetByIdAsync(string id);
    Task<Community?> GetByCommunityIdAsync(string communityId);
    Task<List<Community>> GetActiveCommunitiesAsync();
    Task<List<Community>> GetVisibleCommunitiesAsync();
    Task<Community> CreateAsync(Community community);
    Task UpdateAsync(string id, Community community);
    Task DeleteAsync(string id);
}