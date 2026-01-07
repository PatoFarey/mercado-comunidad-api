using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public class PublicacionService : IPublicacionService
{
    private readonly IMongoCollection<Publication> _publicacionsCollection;

    public PublicacionService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _publicacionsCollection = mongoDatabase.GetCollection<Publication>(
            mongoDbSettings.Value.PublicacionsCollectionName);
    }

    public async Task<List<Publication>> GetAllAsync() =>
        await _publicacionsCollection.Find(_ => true).ToListAsync();

    public async Task<PaginatedResult<Publication>> GetPaginatedAsync(int pageNumber, int pageSize)
    {
        var filter = Builders<Publication>.Filter.Empty;
        var totalCount = await _publicacionsCollection.CountDocumentsAsync(filter);

        var data = await _publicacionsCollection
            .Find(filter)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<Publication>
        {
            Data = data,
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PaginatedResult<Publication>> GetByCategoriaAsync(string categoria, int pageNumber, int pageSize)
    {
        var filter = Builders<Publication>.Filter.Eq(p => p.Categoria, categoria);
        var totalCount = await _publicacionsCollection.CountDocumentsAsync(filter);

        var data = await _publicacionsCollection
            .Find(filter)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<Publication>
        {
            Data = data,
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Publication?> GetByIdAsync(string id) =>
        await _publicacionsCollection.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<Publication> CreateAsync(Publication publicacion)
    {
        await _publicacionsCollection.InsertOneAsync(publicacion);
        return publicacion;
    }

    public async Task UpdateAsync(string id, Publication publicacion) =>
        await _publicacionsCollection.ReplaceOneAsync(p => p.Id == id, publicacion);

    public async Task DeleteAsync(string id) =>
        await _publicacionsCollection.DeleteOneAsync(p => p.Id == id);
}