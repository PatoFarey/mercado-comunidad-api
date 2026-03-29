using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface ITokenService
{
    AuthResponse CreateAuthResponse(UserResponse user);
}
