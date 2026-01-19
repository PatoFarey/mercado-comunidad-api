namespace ApiMercadoComunidad.Models.DTOs;

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
}

public class CategoryResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}