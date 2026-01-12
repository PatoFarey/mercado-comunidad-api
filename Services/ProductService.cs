using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public class ProductService : IProductService
{
    private readonly IMongoCollection<Products> _publicacionsCollection;

    public ProductService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _publicacionsCollection = mongoDatabase.GetCollection<Products>(
            mongoDbSettings.Value.ProductsCollectionName);
    }

    public async Task<List<Products>> GetAllAsync() =>
        await _publicacionsCollection.Find(_ => true).ToListAsync();

    public async Task<PaginatedResult<Products>> GetPaginatedAsync(int pageNumber, int pageSize)
    {
        var filter = Builders<Products>.Filter.Empty;
        var totalCount = await _publicacionsCollection.CountDocumentsAsync(filter);

        var data = await _publicacionsCollection
            .Find(filter)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<Products>
        {
            Data = data,
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResult<Products>> GetByCategoriaAsync(string categoria, int pageNumber, int pageSize)
    {
        var filter = Builders<Products>.Filter.Eq(p => p.Categoria, categoria);
        var totalCount = await _publicacionsCollection.CountDocumentsAsync(filter);

        var data = await _publicacionsCollection
            .Find(filter)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<Products>
        {
            Data = data,
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Products?> GetByIdAsync(string id) =>
        await _publicacionsCollection.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<Products> CreateAsync(Products publicacion)
    {
        await _publicacionsCollection.InsertOneAsync(publicacion);
        return publicacion;
    }

    public async Task UpdateAsync(string id, Products publicacion) =>
        await _publicacionsCollection.ReplaceOneAsync(p => p.Id == id, publicacion);

    public async Task DeleteAsync(string id) =>
        await _publicacionsCollection.DeleteOneAsync(p => p.Id == id);
}