namespace ApiMercadoComunidad.Models.DTOs;

public class ImageUploadRequest
{
    public string Folder { get; set; } = string.Empty; // user, store, community, product
    public string EntityId { get; set; } = string.Empty; // ID de la entidad
    public string? UploadedBy { get; set; } // Opcional: ID del usuario que sube
}

public class ImageUploadResponse
{
    public string Id { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}