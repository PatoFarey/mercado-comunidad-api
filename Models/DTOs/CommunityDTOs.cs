namespace ApiMercadoComunidad.Models.DTOs;

public class StoreCommunityStatusResponse
{
    public string CommunityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public bool Open { get; set; }
    public bool Published { get; set; }
    public bool OwnerEnabled { get; set; }
    public string RequestStatus { get; set; } = string.Empty; // pending | approved | rejected | none
    public string RequestReason { get; set; } = string.Empty;
}

public class PublishStoreRequest
{
    public string StoreId { get; set; } = string.Empty;
    public string CommunityId { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class CommunityRequestResponse
{
    public string Id { get; set; } = string.Empty;
    public string CommunityId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CommunityResponse
{
    public string Id { get; set; } = string.Empty;
    public string CommunityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Open { get; set; }
    public bool Active { get; set; }
    public bool Visible { get; set; }
    public string Logo { get; set; } = string.Empty;
    public string Banner { get; set; } = string.Empty;
    public int StoresCount { get; set; }
    public int ActiveStoresCount { get; set; }
    public int InactiveStoresCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCommunityRequest
{
    public string CommunityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Open { get; set; }
    public bool Active { get; set; } = true;
    public bool Visible { get; set; } = true;
    public string Logo { get; set; } = string.Empty;
}

public class UpdateCommunityRequest
{
    public string? CommunityId { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool? Open { get; set; }
    public bool? Active { get; set; }
    public bool? Visible { get; set; }
    public string? Logo { get; set; }
}
