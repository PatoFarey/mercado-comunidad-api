using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class Sale
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string StoreId { get; set; } = string.Empty;

    [BsonElement("storeName")]
    public string StoreName { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = SaleStatuses.Requested;

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } = "Pago contra entrega o coordinar con vendedor";

    [BsonElement("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [BsonElement("customerEmail")]
    public string CustomerEmail { get; set; } = string.Empty;

    [BsonElement("customerPhone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [BsonElement("customerAddress")]
    public string CustomerAddress { get; set; } = string.Empty;

    [BsonElement("notes")]
    public string Notes { get; set; } = string.Empty;

    [BsonElement("storeObservation")]
    public string StoreObservation { get; set; } = string.Empty;

    [BsonElement("items")]
    public List<SaleItem> Items { get; set; } = new();

    [BsonElement("subtotal")]
    public decimal Subtotal { get; set; }

    [BsonElement("total")]
    public decimal Total { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class SaleItem
{
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("productTitle")]
    public string ProductTitle { get; set; } = string.Empty;

    [BsonElement("productImage")]
    public string ProductImage { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("unitPrice")]
    public decimal UnitPrice { get; set; }

    [BsonElement("lineTotal")]
    public decimal LineTotal { get; set; }
}

public static class SaleStatuses
{
    public const string Requested = "Solicitado";
    public const string InProgress = "En proceso";
    public const string Delivered = "Entregado";
    public const string Cancelled = "Cancelado";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Requested,
        InProgress,
        Delivered,
        Cancelled
    };
}
