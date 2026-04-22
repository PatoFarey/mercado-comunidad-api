using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface IPaykuService
{
    Task<PaykuCreateClientResponse> CreateClientAsync(PaykuCreateClientRequest request);
    Task<PaykuCreatePlanResponse> CreatePlanAsync(PaykuCreatePlanRequest request);
    Task<PaykuCreateSubscriptionResponse> CreateSubscriptionAsync(PaykuCreateSubscriptionRequest request);
}
