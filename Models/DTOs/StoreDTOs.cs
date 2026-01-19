namespace ApiMercadoComunidad.Models.DTOs;

public class CreateStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string LinkStore { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Facebook { get; set; }
    public string? Instagram { get; set; }
    public string? Tiktok { get; set; }
    public string? Website { get; set; }
    public string? UserId { get; set; } // Usuario creador
    public bool IsGlobal { get; set; } = false;
}

public class UpdateStoreRequest
{
    public string? Name { get; set; }
    public string? Dni { get; set; }
    public string? Logo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Facebook { get; set; }
    public string? Instagram { get; set; }
    public string? Tiktok { get; set; }
    public string? Website { get; set; }
    public bool? IsGlobal { get; set; }
}

public class StoreResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Facebook { get; set; } = string.Empty;
    public string Instagram { get; set; } = string.Empty;
    public string Tiktok { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string LinkStore { get; set; } = string.Empty;
    public List<StoreUser> Users { get; set; } = new();
    public bool IsGlobal { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}