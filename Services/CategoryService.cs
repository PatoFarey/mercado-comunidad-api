using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public class CategoryService : ICategoryService
{
    private readonly IMongoCollection<Category> _categoriesCollection;

    public CategoryService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _categoriesCollection = mongoDatabase.GetCollection<Category>("categories");
    }

    public async Task<List<CategoryResponse>> GetAllAsync()
    {
        var categories = await _categoriesCollection
            .Find(_ => true)
            .SortBy(c => c.Name)
            .ToListAsync();

        return categories.Select(MapToCategoryResponse).ToList();
    }

    public async Task<CategoryResponse?> GetByIdAsync(string id)
    {
        var category = await _categoriesCollection
            .Find(c => c.Id == id)
            .FirstOrDefaultAsync();

        return category != null ? MapToCategoryResponse(category) : null;
    }

    public async Task<CategoryResponse?> GetByNameAsync(string name)
    {
        var category = await _categoriesCollection
            .Find(c => c.Name.ToLower() == name.ToLower())
            .FirstOrDefaultAsync();

        return category != null ? MapToCategoryResponse(category) : null;
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request)
    {
        // Verificar si la categoría ya existe
        if (await CategoryExistsAsync(request.Name))
            throw new InvalidOperationException("La categoría ya existe");

        var category = new Category
        {
            Name = request.Name.Trim()
        };

        await _categoriesCollection.InsertOneAsync(category);
        return MapToCategoryResponse(category);
    }

    public async Task<CategoryResponse?> UpdateAsync(string id, UpdateCategoryRequest request)
    {
        // Verificar si el nuevo nombre ya existe en otra categoría
        var existingCategory = await _categoriesCollection
            .Find(c => c.Name.ToLower() == request.Name.ToLower() && c.Id != id)
            .FirstOrDefaultAsync();

        if (existingCategory != null)
            throw new InvalidOperationException("Ya existe una categoría con ese nombre");

        var update = Builders<Category>.Update
            .Set(c => c.Name, request.Name.Trim());

        var result = await _categoriesCollection.UpdateOneAsync(
            c => c.Id == id,
            update
        );

        if (result.ModifiedCount == 0)
            return null;

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _categoriesCollection.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> CategoryExistsAsync(string name)
    {
        var count = await _categoriesCollection
            .CountDocumentsAsync(c => c.Name.ToLower() == name.ToLower());
        return count > 0;
    }

    private CategoryResponse MapToCategoryResponse(Category category)
    {
        return new CategoryResponse
        {
            Id = category.Id ?? string.Empty,
            Name = category.Name
        };
    }
}