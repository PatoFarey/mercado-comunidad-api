using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class MetricEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("productId")]
    [BsonIgnoreIfNull]
    public string? ProductId { get; set; }

    [BsonElement("storeId")]
    [BsonIgnoreIfNull]
    public string? StoreId { get; set; }

    [BsonElement("communityId")]
    [BsonIgnoreIfNull]
    public string? CommunityId { get; set; }

    [BsonElement("productTitle")]
    [BsonIgnoreIfDefault]
    public string ProductTitle { get; set; } = string.Empty;

    [BsonElement("storeName")]
    [BsonIgnoreIfDefault]
    public string StoreName { get; set; } = string.Empty;

    [BsonElement("storeSlug")]
    [BsonIgnoreIfDefault]
    public string StoreSlug { get; set; } = string.Empty;

    [BsonElement("source")]
    [BsonIgnoreIfDefault]
    public string Source { get; set; } = string.Empty;

    [BsonElement("sessionId")]
    [BsonIgnoreIfDefault]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("path")]
    [BsonIgnoreIfDefault]
    public string Path { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public static class MetricEventTypes
{
    public const string Impression = "impression";
    public const string ProductView = "product_view";
    public const string StoreView = "store_view";
    public const string CommunityView = "community_view";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Impression,
        ProductView,
        StoreView,
        CommunityView
    };
}
