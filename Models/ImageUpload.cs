using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class ImageUpload
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("folder")]
    public string Folder { get; set; } = string.Empty; // user, store, community, product

    [BsonElement("entityId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string EntityId { get; set; } = string.Empty; // ID del usuario, tienda, comunidad o producto

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("originalFileName")]
    public string OriginalFileName { get; set; } = string.Empty;

    [BsonElement("blobUrl")]
    public string BlobUrl { get; set; } = string.Empty;

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("fileSizeBytes")]
    public long FileSizeBytes { get; set; }

    [BsonElement("uploadedBy")]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? UploadedBy { get; set; } // ID del usuario que subió la imagen

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
}