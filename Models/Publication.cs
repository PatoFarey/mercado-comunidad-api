using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class Publication
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("longDescription")]
    public string LongDescription { get; set; } = string.Empty;

    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("images")]
    public List<string> Images { get; set; } = new();

    [BsonElement("phone")]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("storeName")]
    public string StoreName { get; set; } = string.Empty;

    [BsonElement("storeDNI")]
    public string StoreDNI { get; set; } = string.Empty;

    [BsonElement("storeLogo")]
    public string StoreLogo { get; set; } = string.Empty;

    [BsonElement("facebookLink")]
    public string FacebookLink { get; set; } = string.Empty;

    [BsonElement("instagramLink")]
    public string InstagramLink { get; set; } = string.Empty;

    [BsonElement("categoria")]
    public string Categoria { get; set; } = string.Empty;
}