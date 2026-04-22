using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class UserSubscription
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("planId")]
    public string PlanId { get; set; } = string.Empty;

    [BsonElement("paykuClientId")]
    public string PaykuClientId { get; set; } = string.Empty;

    [BsonElement("paykuSubscriptionId")]
    [BsonIgnoreIfNull]
    public string? PaykuSubscriptionId { get; set; }

    // pending | active | cancelled | failed
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("startDate")]
    [BsonIgnoreIfNull]
    public DateTime? StartDate { get; set; }

    [BsonElement("nextBillingDate")]
    [BsonIgnoreIfNull]
    public DateTime? NextBillingDate { get; set; }

    [BsonElement("cancelledAt")]
    [BsonIgnoreIfNull]
    public DateTime? CancelledAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
