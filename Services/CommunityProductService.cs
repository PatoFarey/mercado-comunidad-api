using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public class CommunityProductService : ICommunityProductService
{
    private readonly IMongoCollection<CommunityProduct> _communityProductsCollection;
    private readonly IMongoCollection<CommunityStore> _communityStoresCollection;
    private readonly IMongoCollection<Store> _storesCollection;
    private readonly ICommunityService _communityService;

    public CommunityProductService(
        IOptions<MongoDbSettings> mongoDbSettings,
        ICommunityService communityService)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _communityProductsCollection = mongoDatabase.GetCollection<CommunityProduct>("community_products");
        _communityStoresCollection = mongoDatabase.GetCollection<CommunityStore>("community_stores");
        _storesCollection = mongoDatabase.GetCollection<Store>("stores");
        _communityService = communityService;
    }

    private async Task<List<string>> GetActiveStoreIdsAsync(string communityMongoId)
    {
        var communityStoresFilter = Builders<CommunityStore>.Filter.And(
            Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, communityMongoId),
            Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true)
        );
        var communityStores = await _communityStoresCollection.Find(communityStoresFilter).ToListAsync();
        var linkedStoreIds = communityStores.Select(cs => cs.StoreId).ToList();

        if (!linkedStoreIds.Any()) return new List<string>();

        var activeStoreIds = await _storesCollection
            .Find(Builders<Store>.Filter.And(
                Builders<Store>.Filter.In(s => s.Id, linkedStoreIds),
                Builders<Store>.Filter.Eq(s => s.Active, true)
            ))
            .Project(s => s.Id!)
            .ToListAsync();

        return activeStoreIds;
    }

    public async Task<List<CommunityProduct>> GetByCommunityIdAsync(string communityId)
    {
        var community = await _communityService.GetByCommunityIdAsync(communityId);
        if (community == null || string.IsNullOrEmpty(community.Id)) return new List<CommunityProduct>();

        var storeIds = await GetActiveStoreIdsAsync(community.Id);
        if (!storeIds.Any()) return new List<CommunityProduct>();

        var filter = Builders<CommunityProduct>.Filter.And(
            Builders<CommunityProduct>.Filter.In(cp => cp.StoreId, storeIds),
            Builders<CommunityProduct>.Filter.Eq(cp => cp.Active, true)
        );

        return await _communityProductsCollection
            .Find(filter)
            .SortByDescending(cp => cp.CreatedAt)
            .ToListAsync();
    }

    public async Task<PaginatedResult<CommunityProduct>> GetByCommunityIdPaginatedAsync(
        string communityId, int pageNumber, int pageSize)
    {
        var empty = new PaginatedResult<CommunityProduct>
        {
            Data = new List<CommunityProduct>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var community = await _communityService.GetByCommunityIdAsync(communityId);
        if (community == null || string.IsNullOrEmpty(community.Id)) return empty;

        var storeIds = await GetActiveStoreIdsAsync(community.Id);
        if (!storeIds.Any()) return empty;

        var filter = Builders<CommunityProduct>.Filter.And(
            Builders<CommunityProduct>.Filter.In(cp => cp.StoreId, storeIds),
            Builders<CommunityProduct>.Filter.Eq(cp => cp.Active, true)
        );

        var totalCount = await _communityProductsCollection.CountDocumentsAsync(filter);
        var data = await _communityProductsCollection
            .Find(filter)
            .SortByDescending(cp => cp.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<CommunityProduct>
        {
            Data = data,
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<CommunityProduct>> GetByCategoriaAsync(string communityId, string categoria)
    {
        var community = await _communityService.GetByCommunityIdAsync(communityId);
        if (community == null || string.IsNullOrEmpty(community.Id)) return new List<CommunityProduct>();

        var storeIds = await GetActiveStoreIdsAsync(community.Id);
        if (!storeIds.Any()) return new List<CommunityProduct>();

        var filter = Builders<CommunityProduct>.Filter.And(
            Builders<CommunityProduct>.Filter.In(cp => cp.StoreId, storeIds),
            Builders<CommunityProduct>.Filter.Eq(cp => cp.Categoria, categoria),
            Builders<CommunityProduct>.Filter.Eq(cp => cp.Active, true)
        );

        return await _communityProductsCollection
            .Find(filter)
            .SortByDescending(cp => cp.CreatedAt)
            .ToListAsync();
    }

    public async Task<CommunityProduct?> GetByIdAsync(string id)
    {
        return await _communityProductsCollection
            .Find(cp => cp.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<CommunityProduct> CreateAsync(CommunityProduct communityProduct)
    {
        communityProduct.CreatedAt = DateTime.UtcNow;
        await _communityProductsCollection.InsertOneAsync(communityProduct);
        return communityProduct;
    }

    public async Task UpdateAsync(string id, CommunityProduct communityProduct)
    {
        await _communityProductsCollection.ReplaceOneAsync(cp => cp.Id == id, communityProduct);
    }

    public async Task DeleteAsync(string id)
    {
        await _communityProductsCollection.DeleteOneAsync(cp => cp.Id == id);
    }
}