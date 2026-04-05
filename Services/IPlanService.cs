using ApiMercadoComunidad.Models;

namespace ApiMercadoComunidad.Services;

public interface IPlanService
{
    Task<Plan?> GetByIdAsync(string id);
    Task<Plan?> GetDefaultSellerPlanAsync();
    Task<Plan?> GetDefaultCommunityAdminPlanAsync();
    Task<Plan?> GetEffectivePlanForUserAsync(string userId);
    Task<Plan?> GetEffectivePlanForCommunityAdminAsync(string userId);
}
