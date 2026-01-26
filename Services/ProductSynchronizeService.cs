using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public class ProductSynchronizeService : IProductSynchronizeService
{
    private readonly IMongoCollection<Products> _productsCollection;
    private readonly IMongoCollection<CommunityProduct> _communityProductsCollection;
    private readonly IMongoCollection<Store> _storesCollection;
    private readonly IMongoCollection<CommunityStore> _communityStoresCollection;

    public ProductSynchronizeService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _productsCollection = mongoDatabase.GetCollection<Products>("products");
        _communityProductsCollection = mongoDatabase.GetCollection<CommunityProduct>("community_products");
        _storesCollection = mongoDatabase.GetCollection<Store>("stores");
        _communityStoresCollection = mongoDatabase.GetCollection<CommunityStore>("community_stores");
    }

    public async Task<bool> SynchronizeProductAsync(string productId)
    {
        // PASO 1: Obtener el producto desde products
        var product = await _productsCollection
            .Find(p => p.Id == productId)
            .FirstOrDefaultAsync();

        if (product == null)
            return false;

        // PASO 2: Obtener información de la tienda
        var store = await _storesCollection
            .Find(s => s.Id == product.IdStore)
            .FirstOrDefaultAsync();

        if (store == null)
            return false;

        // PASO 3: Insertar o actualizar en community_products
        var communityProduct = new CommunityProduct
        {

            ProductId = product.Id ?? string.Empty,
            StoreId = product.IdStore,
            CreatedAt = product.CreatedAt,

            // Datos del producto
            Title = product.Title,
            Description = product.Description,
            LongDescription = product.LongDescription,
            Price = product.Price,
            Images = product.Images,
            Categoria = product.Category,
            Active = product.Active,

            // Datos de la tienda
            StoreSlug = store.LinkStore,
            StoreName = store.Name,
            StoreLogo = store.Logo,
            FacebookLink = store.Facebook,
            InstagramLink = store.Instagram,
            Phone = store.Phone,
            Web = store.Website,
            Email = store.Email
        };

        // Verificar si ya existe el producto en la coleccion
        var existingFilter = Builders<CommunityProduct>.Filter.Eq(cp => cp.ProductId, product.Id);

        var existing = await _communityProductsCollection
            .Find(existingFilter)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            // Actualizar registro existente manteniendo el _id
            communityProduct.Id = existing.Id;
            await _communityProductsCollection.ReplaceOneAsync(
                cp => cp.Id == existing.Id,
                communityProduct
            );
        }
        else
        {
            // Insertar nuevo registro
            await _communityProductsCollection.InsertOneAsync(communityProduct);
        }


        // PASO 4: Actualizar el campo synchronized en products
        var updateDefinition = Builders<Products>.Update
            .Set(p => p.Synchronized, true)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _productsCollection.UpdateOneAsync(
            p => p.Id == productId,
            updateDefinition
        );

        return true;
    }

    public async Task<int> SynchronizeAllProductsAsync()
    {
        // Obtener todos los productos activos que NO están sincronizados
        var filter = Builders<Products>.Filter.And(
            Builders<Products>.Filter.Eq(p => p.Active, true),
            Builders<Products>.Filter.Eq(p => p.Synchronized, false)
        );

        var products = await _productsCollection
            .Find(filter)
            .ToListAsync();

        int syncCount = 0;

        foreach (var product in products)
        {
            var success = await SynchronizeProductAsync(product.Id ?? string.Empty);
            if (success)
                syncCount++;
        }

        return syncCount;
    }

    public async Task<int> SynchronizeProductsByStoreAsync(string storeId)
    {
        // Obtener todos los productos activos de una tienda
        var products = await _productsCollection
            .Find(p => p.IdStore == storeId && p.Active == true)
            .ToListAsync();

        int syncCount = 0;

        foreach (var product in products)
        {
            var success = await SynchronizeProductAsync(product.Id ?? string.Empty);
            if (success)
                syncCount++;
        }

        return syncCount;
    }
}