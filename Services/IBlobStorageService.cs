using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface IBlobStorageService
{
    Task<ImageUploadResponse> UploadImageAsync(Stream fileStream, string fileName, string contentType, ImageUploadRequest request);
    Task<List<ImageUpload>> GetImagesByEntityAsync(string folder, string entityId);
    Task<ImageUpload?> GetImageByIdAsync(string id);
    Task<bool> DeleteImageAsync(string id);
    Task<bool> DeleteImageByUrlAsync(string blobUrl, string entityId);
    Task<string> GetImageUrlAsync(string blobName);
}