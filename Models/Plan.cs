using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class Plan
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("tier")]
    public string Tier { get; set; } = string.Empty;

    [BsonElement("price_clp")]
    public decimal PriceClp { get; set; }

    [BsonElement("billing")]
    public string Billing { get; set; } = string.Empty;

    [BsonElement("active")]
    public bool Active { get; set; }

    [BsonElement("limits")]
    public PlanLimits Limits { get; set; } = new();

    [BsonElement("features")]
    [BsonIgnoreIfNull]
    public PlanFeatures? Features { get; set; }
}

[BsonIgnoreExtraElements]
public class PlanLimits
{
    // Seller plan limits
    [BsonElement("stores")]
    [BsonIgnoreIfDefault]
    public int Stores { get; set; }

    [BsonElement("products")]
    [BsonIgnoreIfDefault]
    public int Products { get; set; }

    [BsonElement("images_per_product")]
    [BsonIgnoreIfDefault]
    public int ImagesPerProduct { get; set; }

    [BsonElement("video_per_product")]
    [BsonIgnoreIfDefault]
    public bool VideoPerProduct { get; set; }

    [BsonElement("communities_join")]
    [BsonIgnoreIfDefault]
    public int CommunitiesJoin { get; set; }

    // Community admin plan limits
    [BsonElement("communities_create")]
    [BsonIgnoreIfDefault]
    public int CommunitiesCreate { get; set; }

    [BsonElement("sellers_per_community")]
    [BsonIgnoreIfDefault]
    public int SellersPerCommunity { get; set; }
}

[BsonIgnoreExtraElements]
public class PlanFeatures
{
    [BsonElement("featured_in_community")]
    [BsonIgnoreIfDefault]
    public bool FeaturedInCommunity { get; set; }

    [BsonElement("analytics")]
    [BsonIgnoreIfDefault]
    public bool Analytics { get; set; }

    [BsonElement("custom_subdomain")]
    [BsonIgnoreIfDefault]
    public bool CustomSubdomain { get; set; }

    [BsonElement("whatsapp_link")]
    [BsonIgnoreIfDefault]
    public bool WhatsappLink { get; set; }

    [BsonElement("instagram_link")]
    [BsonIgnoreIfDefault]
    public bool InstagramLink { get; set; }

    [BsonElement("priority_support")]
    [BsonIgnoreIfDefault]
    public bool PrioritySupport { get; set; }
}
