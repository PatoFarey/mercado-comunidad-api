using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public class StoreService : IStoreService
{
    private readonly IMongoCollection<Store> _storesCollection;

    public StoreService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _storesCollection = mongoDatabase.GetCollection<Store>("stores");
    }

    public async Task<List<StoreResponse>> GetAllAsync()
    {
        var stores = await _storesCollection.Find(_ => true).ToListAsync();
        return stores.Select(MapToStoreResponse).ToList();
    }

    public async Task<StoreResponse?> GetByIdAsync(string id)
    {
        var store = await _storesCollection
            .Find(s => s.Id == id)
            .FirstOrDefaultAsync();

        return store != null ? MapToStoreResponse(store) : null;
    }

    public async Task<StoreResponse?> GetByLinkStoreAsync(string linkStore)
    {
        var store = await _storesCollection
            .Find(s => s.LinkStore == linkStore.ToLower())
            .FirstOrDefaultAsync();

        return store != null ? MapToStoreResponse(store) : null;
    }

    public async Task<List<StoreResponse>> GetByUserIdAsync(string userId)
    {
        var filter = Builders<Store>.Filter.ElemMatch(
            s => s.Users,
            u => u.UserID == userId
        );

        var stores = await _storesCollection
            .Find(filter)
            .ToListAsync();

        return stores.Select(MapToStoreResponse).ToList();
    }

    public async Task<List<StoreResponse>> GetActiveStoresAsync()
    {
        var stores = await _storesCollection
            .Find(s => s.Active == true)
            .ToListAsync();

        return stores.Select(MapToStoreResponse).ToList();
    }

    public async Task<List<StoreResponse>> GetGlobalStoresAsync()
    {
        var stores = await _storesCollection
            .Find(s => s.IsGlobal == true && s.Active == true)
            .ToListAsync();

        return stores.Select(MapToStoreResponse).ToList();
    }

    public async Task<StoreResponse> CreateAsync(CreateStoreRequest request)
    {
        // Verificar si el link_store ya existe
        if (await LinkStoreExistsAsync(request.LinkStore))
            throw new InvalidOperationException("El link de tienda ya existe");

        var store = new Store
        {
            Name = request.Name,
            Dni = request.Dni,
            LinkStore = request.LinkStore.ToLower(),
            Logo = request.Logo ?? string.Empty,
            Phone = request.Phone ?? string.Empty,
            Email = request.Email ?? string.Empty,
            Facebook = request.Facebook ?? string.Empty,
            Instagram = request.Instagram ?? string.Empty,
            Tiktok = request.Tiktok ?? string.Empty,
            Website = request.Website ?? string.Empty,
            IsGlobal = request.IsGlobal,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Users = new List<StoreUser>()
        };

        // Agregar usuario creador si se proporciona
        if (!string.IsNullOrEmpty(request.UserId))
        {
            store.Users.Add(new StoreUser
            {
                UserID = request.UserId,
                Role = "1" // Role admin/owner
            });
        }

        await _storesCollection.InsertOneAsync(store);
        return MapToStoreResponse(store);
    }

    public async Task<StoreResponse?> UpdateAsync(string id, UpdateStoreRequest request)
    {
        var updateDefinition = Builders<Store>.Update
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(request.Name))
            updateDefinition = updateDefinition.Set(s => s.Name, request.Name);

        if (!string.IsNullOrEmpty(request.Dni))
            updateDefinition = updateDefinition.Set(s => s.Dni, request.Dni);

        if (!string.IsNullOrEmpty(request.Logo))
            updateDefinition = updateDefinition.Set(s => s.Logo, request.Logo);

        if (!string.IsNullOrEmpty(request.Phone))
            updateDefinition = updateDefinition.Set(s => s.Phone, request.Phone);

        if (!string.IsNullOrEmpty(request.Email))
            updateDefinition = updateDefinition.Set(s => s.Email, request.Email);

        if (!string.IsNullOrEmpty(request.Facebook))
            updateDefinition = updateDefinition.Set(s => s.Facebook, request.Facebook);

        if (!string.IsNullOrEmpty(request.Instagram))
            updateDefinition = updateDefinition.Set(s => s.Instagram, request.Instagram);

        if (!string.IsNullOrEmpty(request.Tiktok))
            updateDefinition = updateDefinition.Set(s => s.Tiktok, request.Tiktok);

        if (!string.IsNullOrEmpty(request.Website))
            updateDefinition = updateDefinition.Set(s => s.Website, request.Website);

        if (request.IsGlobal.HasValue)
            updateDefinition = updateDefinition.Set(s => s.IsGlobal, request.IsGlobal.Value);

        var result = await _storesCollection.UpdateOneAsync(
            s => s.Id == id,
            updateDefinition
        );

        if (result.ModifiedCount == 0)
            return null;

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        // Soft delete: marcar como inactivo
        var update = Builders<Store>.Update
            .Set(s => s.Active, false)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var result = await _storesCollection.UpdateOneAsync(
            s => s.Id == id,
            update
        );

        return result.ModifiedCount > 0;
    }

    public async Task<bool> AddUserToStoreAsync(string storeId, string userId, string role)
    {
        var store = await _storesCollection
            .Find(s => s.Id == storeId)
            .FirstOrDefaultAsync();

        if (store == null)
            return false;

        // Verificar si el usuario ya existe en la tienda
        if (store.Users.Any(u => u.UserID == userId))
            return false;

        var newUser = new StoreUser
        {
            UserID = userId,
            Role = role
        };

        var update = Builders<Store>.Update
            .Push(s => s.Users, newUser)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var result = await _storesCollection.UpdateOneAsync(
            s => s.Id == storeId,
            update
        );

        return result.ModifiedCount > 0;
    }

    public async Task<bool> RemoveUserFromStoreAsync(string storeId, string userId)
    {
        var update = Builders<Store>.Update
            .PullFilter(s => s.Users, u => u.UserID == userId)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var result = await _storesCollection.UpdateOneAsync(
            s => s.Id == storeId,
            update
        );

        return result.ModifiedCount > 0;
    }

    public async Task<bool> LinkStoreExistsAsync(string linkStore)
    {
        var count = await _storesCollection
            .CountDocumentsAsync(s => s.LinkStore == linkStore.ToLower());
        return count > 0;
    }

    private StoreResponse MapToStoreResponse(Store store)
    {
        return new StoreResponse
        {
            Id = store.Id ?? string.Empty,
            Name = store.Name,
            Dni = store.Dni,
            Logo = store.Logo,
            Phone = store.Phone,
            Email = store.Email,
            Facebook = store.Facebook,
            Instagram = store.Instagram,
            Tiktok = store.Tiktok,
            Website = store.Website,
            LinkStore = store.LinkStore,
            Users = store.Users,
            IsGlobal = store.IsGlobal,
            Active = store.Active,
            CreatedAt = store.CreatedAt,
            UpdatedAt = store.UpdatedAt
        };
    }
}