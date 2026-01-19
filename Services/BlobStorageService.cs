using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly IMongoCollection<ImageUpload> _imagesCollection;
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IMongoCollection<Store> _storesCollection;
    private readonly IMongoCollection<Community> _communitiesCollection;
    private readonly IProductService _productService;
    private readonly string _containerName;

    public BlobStorageService(
        IOptions<AzureBlobSettings> azureBlobSettings,
        IOptions<MongoDbSettings> mongoDbSettings,
        IProductService productService)
    {
        _blobServiceClient = new BlobServiceClient(azureBlobSettings.Value.ConnectionString);
        _containerName = azureBlobSettings.Value.ContainerName;
        _containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        
        // Crear el contenedor si no existe
        _containerClient.CreateIfNotExists(PublicAccessType.Blob);

        // MongoDB
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _imagesCollection = mongoDatabase.GetCollection<ImageUpload>("images");
        _usersCollection = mongoDatabase.GetCollection<User>("users");
        _storesCollection = mongoDatabase.GetCollection<Store>("stores");
        _communitiesCollection = mongoDatabase.GetCollection<Community>("communities");
        
        // Inyectar ProductService
        _productService = productService;
    }

    public async Task<ImageUploadResponse> UploadImageAsync(
        Stream fileStream, 
        string fileName, 
        string contentType, 
        ImageUploadRequest request)
    {
        // Validar folder
        var validFolders = new[] { "user", "store", "community", "product" };
        if (!validFolders.Contains(request.Folder.ToLower()))
            throw new ArgumentException($"Folder debe ser uno de: {string.Join(", ", validFolders)}");

        // PASO 1: Buscar y eliminar imagen anterior si existe (solo si NO es temporal)
        if (!request.EntityId.StartsWith("temp-", StringComparison.OrdinalIgnoreCase))
        {
            var existingImageFilter = Builders<ImageUpload>.Filter.And(
                Builders<ImageUpload>.Filter.Eq(i => i.Folder, request.Folder.ToLower()),
                Builders<ImageUpload>.Filter.Eq(i => i.EntityId, request.EntityId),
                Builders<ImageUpload>.Filter.Eq(i => i.IsActive, true)
            );

            var existingImage = await _imagesCollection
                .Find(existingImageFilter)
                .FirstOrDefaultAsync();

            if (existingImage != null)
            {
                // Eliminar del Blob Storage
                var oldBlobName = $"{existingImage.Folder}/{existingImage.FileName}";
                var oldBlobClient = _containerClient.GetBlobClient(oldBlobName);
                await oldBlobClient.DeleteIfExistsAsync();

                // Eliminar de MongoDB
                await _imagesCollection.DeleteOneAsync(i => i.Id == existingImage.Id);
            }
        }

        // PASO 2: Generar nombre único para el nuevo archivo
        var fileExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var blobName = $"{request.Folder.ToLower()}/{uniqueFileName}";

        // PASO 3: Subir a Azure Blob Storage
        var blobClient = _containerClient.GetBlobClient(blobName);
        
        var blobHttpHeaders = new BlobHttpHeaders
        {
            ContentType = contentType
        };

        await blobClient.UploadAsync(fileStream, new BlobUploadOptions
        {
            HttpHeaders = blobHttpHeaders
        });

        // Obtener URL del blob
        var blobUrl = blobClient.Uri.ToString();

        // PASO 4: Guardar metadatos en MongoDB
        var imageUpload = new ImageUpload
        {
            Folder = request.Folder.ToLower(),
            EntityId = request.EntityId,
            FileName = uniqueFileName,
            OriginalFileName = fileName,
            BlobUrl = blobUrl,
            ContentType = contentType,
            FileSizeBytes = fileStream.Length,
            UploadedBy = request.UploadedBy,
            CreatedAt = DateTime.UtcNow,    
            IsActive = true
        };

        await _imagesCollection.InsertOneAsync(imageUpload);

        // PASO 5: Actualizar la entidad correspondiente según el folder (solo si NO es temporal)
        if (!request.EntityId.StartsWith("temp-", StringComparison.OrdinalIgnoreCase))
        {
            switch (request.Folder.ToLower())
            {
                case "user":
                    var userUpdate = Builders<User>.Update
                        .Set(u => u.Avatar, blobUrl)
                        .Set(u => u.UpdatedAt, DateTime.UtcNow);

                    await _usersCollection.UpdateOneAsync(
                        u => u.Id == request.EntityId,
                        userUpdate
                    );
                    break;

                case "store":
                    var storeUpdate = Builders<Store>.Update
                        .Set(s => s.Logo, blobUrl)
                        .Set(s => s.UpdatedAt, DateTime.UtcNow);

                    await _storesCollection.UpdateOneAsync(
                        s => s.Id == request.EntityId,
                        storeUpdate
                    );
                    break;

                case "product":
                    // Agregar imagen al array de imágenes del producto
                    await _productService.AddImageAsync(request.EntityId, blobUrl);
                    break;
            }
        }

        return new ImageUploadResponse
        {
            Id = imageUpload.Id ?? string.Empty,
            Folder = imageUpload.Folder,
            EntityId = imageUpload.EntityId,
            FileName = imageUpload.FileName,
            BlobUrl = imageUpload.BlobUrl,
            ContentType = imageUpload.ContentType,
            FileSizeBytes = imageUpload.FileSizeBytes,
            CreatedAt = imageUpload.CreatedAt
        };
    }

    public async Task<List<ImageUpload>> GetImagesByEntityAsync(string folder, string entityId)
    {
        var filter = Builders<ImageUpload>.Filter.And(
            Builders<ImageUpload>.Filter.Eq(i => i.Folder, folder.ToLower()),
            Builders<ImageUpload>.Filter.Eq(i => i.EntityId, entityId),
            Builders<ImageUpload>.Filter.Eq(i => i.IsActive, true)
        );

        return await _imagesCollection
            .Find(filter)
            .SortByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<ImageUpload?> GetImageByIdAsync(string id)
    {
        return await _imagesCollection
            .Find(i => i.Id == id && i.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> DeleteImageAsync(string id)
    {
        var image = await GetImageByIdAsync(id);
        if (image == null)
            return false;

        // Eliminar del Blob Storage
        var blobName = $"{image.Folder}/{image.FileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();

        // Si es una imagen de producto, también eliminarla del array
        if (image.Folder == "product" && !image.EntityId.StartsWith("temp-", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _productService.RemoveImageAsync(image.EntityId, image.BlobUrl);
            }
            catch (InvalidOperationException)
            {
                // La imagen ya no existe en el producto, continuar con el soft delete
            }
        }

        // Marcar como inactivo en MongoDB (soft delete)
        var update = Builders<ImageUpload>.Update.Set(i => i.IsActive, false);
        var result = await _imagesCollection.UpdateOneAsync(i => i.Id == id, update);

        return result.ModifiedCount > 0;
    }

    public async Task<string> GetImageUrlAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        return blobClient.Uri.ToString();
    }
}