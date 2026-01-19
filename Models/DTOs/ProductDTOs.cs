namespace ApiMercadoComunidad.Models.DTOs;

public class CreateProductRequest
{
    public string IdStore { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<string> Images { get; set; } = new();
    public string Category { get; set; } = string.Empty;
}

public class UpdateProductRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? LongDescription { get; set; }
    public decimal? Price { get; set; }
    public List<string>? Images { get; set; }
    public string? Category { get; set; }
    public bool? Active { get; set; }
}

public class ProductResponse
{
    public string Id { get; set; } = string.Empty;
    public string IdStore { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<string> Images { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Nuevos DTOs para gestión de imágenes
public class AddImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
}

public class ReorderImagesRequest
{
    public List<string> Images { get; set; } = new();
}