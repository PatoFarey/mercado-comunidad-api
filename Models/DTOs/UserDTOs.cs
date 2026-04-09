namespace ApiMercadoComunidad.Models.DTOs;

public class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    /// <summary>buyer (default) or user (seller)</summary>
    public string? Role { get; set; }
}

public class UpdateRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
    public Address? Address { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class RequestPasswordResetRequest
{
    public string Email { get; set; } = string.Empty;
}

public class RequestEmailVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ValidateResetCodeRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public string Phone { get; set; } = string.Empty;
    public Address? Address { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? PlanId { get; set; }
    public PlanSummaryResponse? Plan { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserResponse User { get; set; } = null!;
}

public class PlanSummaryResponse
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public bool Active { get; set; }
    public PlanLimitsResponse Limits { get; set; } = new();
}

public class PlanLimitsResponse
{
    public int Stores { get; set; } = -1;
    public int Products { get; set; } = -1;
    public int ImagesPerProduct { get; set; } = -1;
    public bool VideoPerProduct { get; set; }
    public int CommunitiesJoin { get; set; } = -1;
    public int CommunitiesCreate { get; set; } = -1;
    public int SellersPerCommunity { get; set; } = -1;
}

public class ApproveRejectRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
