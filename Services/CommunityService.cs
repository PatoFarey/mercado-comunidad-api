using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public class CommunityService : ICommunityService
{
    private readonly IMongoCollection<Community> _communitiesCollection;

    public CommunityService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _communitiesCollection = mongoDatabase.GetCollection<Community>("communities");
    }

    public async Task<List<Community>> GetAllAsync() =>
        await _communitiesCollection.Find(_ => true).ToListAsync();

    public async Task<Community?> GetByIdAsync(string id) =>
        await _communitiesCollection.Find(c => c.Id == id).FirstOrDefaultAsync();

    public async Task<Community?> GetByCommunityIdAsync(string communityId) =>
        await _communitiesCollection.Find(c => c.CommunityId == communityId).FirstOrDefaultAsync();

    public async Task<List<Community>> GetActiveCommunitiesAsync() =>
        await _communitiesCollection.Find(c => c.Active == true).ToListAsync();

    public async Task<List<Community>> GetVisibleCommunitiesAsync() =>
        await _communitiesCollection.Find(c => c.Visible== true).ToListAsync();

    public async Task<Community> CreateAsync(Community community)
    {
        await _communitiesCollection.InsertOneAsync(community);
        return community;
    }

    public async Task UpdateAsync(string id, Community community) =>
        await _communitiesCollection.ReplaceOneAsync(c => c.Id == id, community);

    public async Task DeleteAsync(string id) =>
        await _communitiesCollection.DeleteOneAsync(c => c.Id == id);
}