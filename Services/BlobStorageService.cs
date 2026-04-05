using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ApiMercadoComunidad.Services;

public class BlobStorageService : IBlobStorageService
{
    private static readonly string[] ValidFolders = ["user", "store", "community", "product"];

    private readonly IAmazonS3 _s3Client;
    private readonly IMongoCollection<ImageUpload> _imagesCollection;
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IMongoCollection<Store> _storesCollection;
    private readonly IMongoCollection<Community> _communitiesCollection;
    private readonly IProductService _productService;
    private readonly string _bucketName;
    private readonly string _publicBaseUrl;

    public BlobStorageService(
        IOptions<R2StorageSettings> r2StorageSettings,
        IOptions<MongoDbSettings> mongoDbSettings,
        IProductService productService)
    {
        var settings = r2StorageSettings.Value;

        if (string.IsNullOrWhiteSpace(settings.AccountId) ||
            string.IsNullOrWhiteSpace(settings.AccessKeyId) ||
            string.IsNullOrWhiteSpace(settings.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(settings.BucketName) ||
            string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
        {
            throw new InvalidOperationException("R2StorageSettings is incomplete. Configure AccountId, AccessKeyId, SecretAccessKey, BucketName and PublicBaseUrl.");
        }

        _bucketName = settings.BucketName;
        _publicBaseUrl = settings.PublicBaseUrl.TrimEnd('/');

        var credentials = new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{settings.AccountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client(credentials, config);

        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _imagesCollection = mongoDatabase.GetCollection<ImageUpload>("images");
        _usersCollection = mongoDatabase.GetCollection<User>("users");
        _storesCollection = mongoDatabase.GetCollection<Store>("stores");
        _communitiesCollection = mongoDatabase.GetCollection<Community>("communities");
        _productService = productService;
    }

    public async Task<ImageUploadResponse> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        ImageUploadRequest request)
    {
        var normalizedFolder = request.Folder.ToLowerInvariant();
        if (!ValidFolders.Contains(normalizedFolder))
        {
            throw new ArgumentException($"Folder debe ser uno de: {string.Join(", ", ValidFolders)}");
        }

        // Solo reemplazar imagen existente para entidades de imagen única (user, store).
        // Los productos admiten múltiples imágenes, por lo que no se elimina la anterior.
        var singleImageFolders = new[] { "user", "store" };
        if (!request.EntityId.StartsWith("temp-", StringComparison.OrdinalIgnoreCase)
            && singleImageFolders.Contains(normalizedFolder))
        {
            var existingImageFilter = Builders<ImageUpload>.Filter.And(
                Builders<ImageUpload>.Filter.Eq(i => i.Folder, normalizedFolder),
                Builders<ImageUpload>.Filter.Eq(i => i.EntityId, request.EntityId),
                Builders<ImageUpload>.Filter.Eq(i => i.IsActive, true)
            );

            var existingImage = await _imagesCollection
                .Find(existingImageFilter)
                .FirstOrDefaultAsync();

            if (existingImage != null)
            {
                await DeleteObjectAsync(GetObjectKey(existingImage));
                await _imagesCollection.DeleteOneAsync(i => i.Id == existingImage.Id);
            }
        }

        var (processedStream, processedContentType, processedExtension) = await ProcessImageAsync(fileStream);
        await using var _ = processedStream;

        var uniqueFileName = $"{Guid.NewGuid()}{processedExtension}";
        var objectKey = BuildObjectKey(normalizedFolder, request.EntityId, uniqueFileName);

        var uploadRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = processedStream,
            ContentType = processedContentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await _s3Client.PutObjectAsync(uploadRequest);

        var blobUrl = BuildPublicUrl(objectKey);
        var fileSizeBytes = processedStream.CanSeek ? processedStream.Length : 0;

        var imageUpload = new ImageUpload
        {
            Folder = normalizedFolder,
            EntityId = request.EntityId,
            FileName = uniqueFileName,
            ObjectKey = objectKey,
            OriginalFileName = fileName,
            BlobUrl = blobUrl,
            ContentType = processedContentType,
            FileSizeBytes = fileSizeBytes,
            UploadedBy = request.UploadedBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _imagesCollection.InsertOneAsync(imageUpload);

        if (!request.EntityId.StartsWith("temp-", StringComparison.OrdinalIgnoreCase))
        {
            switch (normalizedFolder)
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
        {
            return false;
        }

        await DeleteObjectAsync(GetObjectKey(image));

        if (image.Folder == "product" && !image.EntityId.StartsWith("temp-", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _productService.RemoveImageAsync(image.EntityId, image.BlobUrl);
            }
            catch (InvalidOperationException)
            {
            }
        }

        var update = Builders<ImageUpload>.Update.Set(i => i.IsActive, false);
        var result = await _imagesCollection.UpdateOneAsync(i => i.Id == id, update);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteImageByUrlAsync(string blobUrl, string entityId)
    {
        var filter = Builders<ImageUpload>.Filter.And(
            Builders<ImageUpload>.Filter.Eq(i => i.BlobUrl, blobUrl),
            Builders<ImageUpload>.Filter.Eq(i => i.EntityId, entityId),
            Builders<ImageUpload>.Filter.Eq(i => i.IsActive, true)
        );

        var image = await _imagesCollection
            .Find(filter)
            .FirstOrDefaultAsync();

        if (image == null)
        {
            return false;
        }

        await DeleteObjectAsync(GetObjectKey(image));

        if (image.Folder == "product")
        {
            try
            {
                await _productService.RemoveImageAsync(image.EntityId, image.BlobUrl);
            }
            catch (InvalidOperationException)
            {
            }
        }

        var result = await _imagesCollection.DeleteOneAsync(i => i.Id == image.Id);
        return result.DeletedCount > 0;
    }

    public Task<string> GetImageUrlAsync(string blobName)
    {
        return Task.FromResult(BuildPublicUrl(blobName));
    }

    private string BuildObjectKey(string folder, string entityId, string fileName)
    {
        return $"{folder.ToLowerInvariant()}/{entityId}/{fileName}";
    }

    private string GetObjectKey(ImageUpload image)
    {
        if (!string.IsNullOrWhiteSpace(image.ObjectKey))
        {
            return image.ObjectKey;
        }

        return BuildObjectKey(image.Folder, image.EntityId, image.FileName);
    }

    private string BuildPublicUrl(string objectKey)
    {
        return $"{_publicBaseUrl}/{objectKey}";
    }

    private static async Task<(Stream processedStream, string contentType, string extension)> ProcessImageAsync(Stream input)
    {
        const int MaxDimension = 1200;
        const int WebpQuality = 82;

        using var image = await Image.LoadAsync(input);

        if (image.Width > MaxDimension || image.Height > MaxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(MaxDimension, MaxDimension),
                Mode = ResizeMode.Max
            }));
        }

        var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = WebpQuality });
        output.Position = 0;

        return (output, "image/webp", ".webp");
    }

    private async Task DeleteObjectAsync(string objectKey)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            });
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
            string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            // Si el archivo no existe en storage, no debe bloquear la limpieza en BD.
        }
    }
}
