using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface IUserService
{
    Task<UserResponse?> RegisterAsync(RegisterRequest request);
    Task<UserResponse?> LoginAsync(LoginRequest request);
    Task<UserResponse?> GetByIdAsync(string id);
    Task<UserResponse?> GetByEmailAsync(string email);
    Task<UserResponse?> UpdateUserAsync(string id, UpdateUserRequest request);
    Task<bool> ChangePasswordAsync(string id, ChangePasswordRequest request);
    Task<bool> DeleteUserAsync(string id);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> VerifyEmailAsync(string userId);
    Task<bool> RequestEmailVerificationAsync(string email);
    Task<bool> RequestPasswordResetAsync(string email);
    Task<bool> ValidateResetCodeAsync(string email, string code);
    Task<bool> ResetPasswordAsync(string email, string code, string newPassword);
    Task<bool> UpdateRoleAsync(string id, string role);
    Task<List<UserResponse>> GetAllAsync();
}
