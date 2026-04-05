using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public class CommunityService : ICommunityService
{
    private readonly IMongoCollection<Community> _communitiesCollection;
    private readonly IMongoCollection<CommunityStore> _communityStoresCollection;

    public CommunityService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _communitiesCollection = mongoDatabase.GetCollection<Community>("communities");
        _communityStoresCollection = mongoDatabase.GetCollection<CommunityStore>("community_stores");
    }

    public async Task<List<Community>> GetAllAsync() =>
        await _communitiesCollection.Find(_ => true).ToListAsync();

    public async Task<List<CommunityResponse>> GetAllWithStatsAsync()
    {
        var communities = await _communitiesCollection.Find(_ => true).ToListAsync();
        var responses = new List<CommunityResponse>();

        foreach (var c in communities)
        {
            var filter = Builders<CommunityStore>.Filter.And(
                Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, c.Id),
                Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true)
            );
            var storesCount = (int)await _communityStoresCollection.CountDocumentsAsync(filter);

            responses.Add(MapToResponse(c, storesCount));
        }

        return responses;
    }

    public async Task<Community?> GetByIdAsync(string id) =>
        await _communitiesCollection.Find(c => c.Id == id).FirstOrDefaultAsync();

    public async Task<Community?> GetByCommunityIdAsync(string communityId) =>
        await _communitiesCollection.Find(c => c.CommunityId == communityId).FirstOrDefaultAsync();

    public async Task<List<CommunityResponse>> GetByOwnerWithStatsAsync(string? ownerUserId, string? ownerEmail, bool includeAll = false)
    {
        var communities = await _communitiesCollection.Find(_ => true).ToListAsync();

        if (!includeAll)
        {
            communities = communities
                .Where(c =>
                    (!string.IsNullOrWhiteSpace(ownerUserId) && c.OwnerUserId == ownerUserId) ||
                    (string.IsNullOrWhiteSpace(c.OwnerUserId) &&
                     !string.IsNullOrWhiteSpace(ownerEmail) &&
                     string.Equals(c.Email, ownerEmail, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var responses = new List<CommunityResponse>();
        foreach (var c in communities)
        {
            var filter = Builders<CommunityStore>.Filter.And(
                Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, c.Id),
                Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true)
            );
            var storesCount = (int)await _communityStoresCollection.CountDocumentsAsync(filter);
            responses.Add(MapToResponse(c, storesCount));
        }

        return responses;
    }

    public async Task<List<Community>> GetActiveCommunitiesAsync() =>
        await _communitiesCollection.Find(c => c.Active == true).ToListAsync();

    public async Task<List<Community>> GetVisibleCommunitiesAsync() =>
        await _communitiesCollection.Find(c => c.Visible == true).ToListAsync();

    public async Task<List<CommunityResponse>> GetVisibleWithStatsAsync()
    {
        var communities = await _communitiesCollection.Find(c => c.Visible == true).ToListAsync();
        var responses = new List<CommunityResponse>();

        foreach (var c in communities)
        {
            var filter = Builders<CommunityStore>.Filter.And(
                Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, c.Id),
                Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true)
            );
            var storesCount = (int)await _communityStoresCollection.CountDocumentsAsync(filter);
            responses.Add(MapToResponse(c, storesCount));
        }

        return responses;
    }

    public async Task<Community> CreateAsync(Community community)
    {
        await _communitiesCollection.InsertOneAsync(community);
        return community;
    }

    public async Task UpdateAsync(string id, Community community) =>
        await _communitiesCollection.ReplaceOneAsync(c => c.Id == id, community);

    public async Task DeleteAsync(string id) =>
        await _communitiesCollection.DeleteOneAsync(c => c.Id == id);

    private static CommunityResponse MapToResponse(Community c, int storesCount) => new()
    {
        Id = c.Id ?? string.Empty,
        CommunityId = c.CommunityId,
        Name = c.Name,
        Title = c.Title,
        Description = c.Description,
        Phone = c.Phone,
        Email = c.Email,
        Open = c.Open,
        Active = c.Active,
        Visible = c.Visible,
        Logo = c.Logo,
        StoresCount = storesCount,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
