using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public class ProductService : IProductService
{
    private readonly IMongoCollection<Products> _productsCollection;
    private readonly IProductSynchronizeService _synchronizeService;

    public ProductService(IOptions<MongoDbSettings> mongoDbSettings, IProductSynchronizeService synchronizeService)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _productsCollection = mongoDatabase.GetCollection<Products>(
            mongoDbSettings.Value.ProductsCollectionName);
        _synchronizeService = synchronizeService;
    }

    public async Task<PaginatedResult<ProductResponse>> GetPaginatedAsync(int pageNumber, int pageSize)
    {
        var filter = Builders<Products>.Filter.Eq(p => p.Active, true);
        var totalCount = await _productsCollection.CountDocumentsAsync(filter);

        var products = await _productsCollection
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<ProductResponse>
        {
            Data = products.Select(MapToProductResponse).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ProductResponse?> GetByIdAsync(string id)
    {
        var product = await _productsCollection
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        return product != null ? MapToProductResponse(product) : null;
    }

    public async Task<PaginatedResult<ProductResponse>> GetByCategoryAsync(string categoria, int pageNumber, int pageSize)
    {
        var filter = Builders<Products>.Filter.And(
            Builders<Products>.Filter.Eq(p => p.Category, categoria),
            Builders<Products>.Filter.Eq(p => p.Active, true)
        );

        var totalCount = await _productsCollection.CountDocumentsAsync(filter);

        var products = await _productsCollection
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<ProductResponse>
        {
            Data = products.Select(MapToProductResponse).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<ProductResponse>> GetByStoreIdAsync(string storeId)
    {
        var filter = Builders<Products>.Filter.And(
            Builders<Products>.Filter.Eq(p => p.IdStore, storeId),
            Builders<Products>.Filter.Eq(p => p.Active, true)
        );

        var products = await _productsCollection
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(MapToProductResponse).ToList();
    }

    public async Task<PaginatedResult<ProductResponse>> GetByStoreIdPaginatedAsync(string storeId, int pageNumber, int pageSize)
    {
        var filter = Builders<Products>.Filter.And(
            Builders<Products>.Filter.Eq(p => p.IdStore, storeId)
            //Builders<Products>.Filter.Eq(p => p.Active, true)
        );

        var totalCount = await _productsCollection.CountDocumentsAsync(filter);

        var products = await _productsCollection
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<ProductResponse>
        {
            Data = products.Select(MapToProductResponse).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<ProductResponse>> GetActiveProductsAsync()
    {
        var products = await _productsCollection
            .Find(p => p.Active == true)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(MapToProductResponse).ToList();
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        var product = new Products
        {
            IdStore = request.IdStore,
            Title = request.Title,
            Description = request.Description,
            LongDescription = request.LongDescription,
            Price = request.Price,
            Images = request.Images,
            Category = request.Category,
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _productsCollection.InsertOneAsync(product);

        // Sincronizar automáticamente
        await _synchronizeService.SynchronizeProductAsync(product.Id ?? string.Empty);

        return MapToProductResponse(product);
    }

    public async Task<ProductResponse?> UpdateAsync(string id, UpdateProductRequest request)
    {
        var updateDefinition = Builders<Products>.Update
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(request.Title))
            updateDefinition = updateDefinition.Set(p => p.Title, request.Title);

        if (!string.IsNullOrEmpty(request.Description))
            updateDefinition = updateDefinition.Set(p => p.Description, request.Description);

        if (!string.IsNullOrEmpty(request.LongDescription))
            updateDefinition = updateDefinition.Set(p => p.LongDescription, request.LongDescription);

        if (request.Price.HasValue)
            updateDefinition = updateDefinition.Set(p => p.Price, request.Price.Value);

        if (request.Images != null && request.Images.Any())
            updateDefinition = updateDefinition.Set(p => p.Images, request.Images);

        if (!string.IsNullOrEmpty(request.Category))
            updateDefinition = updateDefinition.Set(p => p.Category, request.Category);

        if (request.Active.HasValue)
            updateDefinition = updateDefinition.Set(p => p.Active, request.Active.Value);

        var result = await _productsCollection.UpdateOneAsync(
            p => p.Id == id,
            updateDefinition
        );

        if (result.ModifiedCount == 0)
            return null;

        // Sincronizar automáticamente después de actualizar
        await _synchronizeService.SynchronizeProductAsync(id);

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        // Soft delete: marcar como inactivo
        var update = Builders<Products>.Update
            .Set(p => p.Active, false)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        var result = await _productsCollection.UpdateOneAsync(
            p => p.Id == id,
            update
        );

        return result.ModifiedCount > 0;
    }

    private ProductResponse MapToProductResponse(Products product)
    {
        return new ProductResponse
        {
            Id = product.Id ?? string.Empty,
            IdStore = product.IdStore,
            Title = product.Title,
            Description = product.Description,
            LongDescription = product.LongDescription,
            Price = product.Price,
            Images = product.Images,
            Category = product.Category,
            Active = product.Active,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    public async Task<ProductResponse?> AddImageAsync(string id, string imageUrl)
    {
        var product = await _productsCollection
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (product == null)
            return null;

        // Verificar si la imagen ya existe
        if (product.Images.Contains(imageUrl))
            throw new InvalidOperationException("La imagen ya existe en el producto");

        // Agregar la nueva imagen al array
        var update = Builders<Products>.Update
            .Push(p => p.Images, imageUrl)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _productsCollection.UpdateOneAsync(p => p.Id == id, update);

        // Sincronizar para actualizar las imágenes en community_products
        await _synchronizeService.SynchronizeProductAsync(id);

        return await GetByIdAsync(id);
    }

    public async Task<ProductResponse?> RemoveImageAsync(string id, string imageUrl)
    {
        var product = await _productsCollection
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (product == null)
            return null;

        // Verificar si la imagen existe
        if (!product.Images.Contains(imageUrl))
            throw new InvalidOperationException("La imagen no existe en el producto");

        // Remover la imagen del array
        var update = Builders<Products>.Update
            .Pull(p => p.Images, imageUrl)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _productsCollection.UpdateOneAsync(p => p.Id == id, update);

        return await GetByIdAsync(id);
    }

    public async Task<ProductResponse?> ReorderImagesAsync(string id, List<string> images)
    {
        var product = await _productsCollection
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        if (product == null)
            return null;

        // Validar que todas las imágenes existan en el producto
        var missingImages = images.Except(product.Images).ToList();
        if (missingImages.Any())
            throw new InvalidOperationException($"Las siguientes imágenes no existen en el producto: {string.Join(", ", missingImages)}");

        // Actualizar el orden de las imágenes
        var update = Builders<Products>.Update
            .Set(p => p.Images, images)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _productsCollection.UpdateOneAsync(p => p.Id == id, update);

        return await GetByIdAsync(id);
    }
}