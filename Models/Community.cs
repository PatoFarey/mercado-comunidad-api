using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class Community
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("id")]
    public string CommunityId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("users")]
    public List<UserReference> Users { get; set; } = new();

    [BsonElement("open")]
    public bool Open { get; set; }

    [BsonElement("stores")]
    public List<StoreReference> Stores { get; set; } = new();

    [BsonElement("active")]
    public bool Active { get; set; }

    [BsonElement("banner")]
    public string Banner { get; set; } = string.Empty;

    [BsonElement("logo")]
    public string Logo { get; set; } = string.Empty;
}

public class StoreReference
{
    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string StoreId { get; set; } = string.Empty;

    [BsonElement("status")]
    public bool Status { get; set; }
}

public class UserReference
{
    [BsonElement("userID")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserID { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;
}