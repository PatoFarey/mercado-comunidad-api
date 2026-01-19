using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class Store
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("dni")]
    public string Dni { get; set; } = string.Empty;

    [BsonElement("logo")]
    [BsonIgnoreIfDefault]
    public string Logo { get; set; } = string.Empty;

    [BsonElement("facebook")]
    [BsonIgnoreIfDefault]
    public string Facebook { get; set; } = string.Empty;

    [BsonElement("instagram")]
    [BsonIgnoreIfDefault]
    public string Instagram { get; set; } = string.Empty;

    [BsonElement("tiktok")]
    [BsonIgnoreIfDefault]
    public string Tiktok { get; set; } = string.Empty;

    [BsonElement("website")]
    [BsonIgnoreIfDefault]
    public string Website { get; set; } = string.Empty;

    [BsonElement("phone")]
    [BsonIgnoreIfDefault]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("email")]
    [BsonIgnoreIfDefault]
    public string Email { get; set; } = string.Empty;

    [BsonElement("link_store")]
    public string LinkStore { get; set; } = string.Empty;

    [BsonElement("users")]
    public List<StoreUser> Users { get; set; } = new();

    [BsonElement("isGlobal")]
    public bool IsGlobal { get; set; } = false;

    [BsonElement("active")]
    public bool Active { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class StoreUser
{
    [BsonElement("userID")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserID { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;
}