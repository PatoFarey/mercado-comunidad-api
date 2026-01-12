using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace ApiMercadoComunidad.Models
{
    public class CommunityProduct
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("communityId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string CommunityId { get; set; } = string.Empty;

        [BsonElement("productId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("storeId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string StoreId { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }

        // Campos denormalizados para render rápido
        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("price")]
        public decimal Price { get; set; }

        [BsonElement("images")]
        public List<string> Images { get; set; } = new();

        [BsonElement("categoria")]
        public string Categoria { get; set; } = string.Empty;

        [BsonElement("storeSlug")]
        public string StoreSlug { get; set; } = string.Empty;

        [BsonElement("storeName")]
        public string StoreName { get; set; } = string.Empty;

        [BsonElement("storeLogo")]
        public string StoreLogo { get; set; } = string.Empty;

        [BsonElement("active")]
        public bool Active { get; set; } = true;

        [BsonElement("facebookLink")]
        public string FacebookLink { get; set; } = string.Empty;

        [BsonElement("instagramLink")]
        public string InstagramLink { get; set; } = string.Empty;

        [BsonElement("phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("web")]
        public string Web { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("longDescription")]
        public string LongDescription { get; set; } = string.Empty;

    }
}
