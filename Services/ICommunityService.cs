using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface ICommunityService
{
    Task<List<Community>> GetAllAsync();
    Task<List<CommunityResponse>> GetAllWithStatsAsync();
    Task<Community?> GetByIdAsync(string id);
    Task<Community?> GetByCommunityIdAsync(string communityId);
    Task<List<CommunityResponse>> GetByOwnerWithStatsAsync(string? ownerUserId, string? ownerEmail, bool includeAll = false);
    Task<List<Community>> GetActiveCommunitiesAsync();
    Task<List<Community>> GetVisibleCommunitiesAsync();
    Task<List<CommunityResponse>> GetVisibleWithStatsAsync();
    Task<Community> CreateAsync(Community community);
    Task UpdateAsync(string id, Community community);
    Task DeleteAsync(string id);
}
