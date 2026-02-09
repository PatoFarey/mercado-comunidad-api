using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ApiMercadoComunidad.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("password")]
    public string Password { get; set; } = string.Empty;

    [BsonElement("avatar")]
    [BsonIgnoreIfDefault]
    public string Avatar { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = "user";

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("emailVerified")]
    public bool EmailVerified { get; set; } = false;

    [BsonElement("phone")]
    [BsonIgnoreIfDefault]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("address")]
    [BsonIgnoreIfNull]
    public Address? Address { get; set; }

    [BsonElement("lastLogin")]
    [BsonIgnoreIfNull]
    public DateTime? LastLogin { get; set; }

    [BsonElement("passwordResetCode")]
    [BsonIgnoreIfNull]
    public string? PasswordResetCode { get; set; }

    [BsonElement("passwordResetExpiry")]
    [BsonIgnoreIfNull]
    public DateTime? PasswordResetExpiry { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class Address
{
    [BsonElement("street")]
    public string Street { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("state")]
    public string State { get; set; } = string.Empty;

    [BsonElement("zipCode")]
    public string ZipCode { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;
}