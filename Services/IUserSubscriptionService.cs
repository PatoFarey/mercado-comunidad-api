using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface IUserSubscriptionService
{
    Task<UserSubscription?> GetByUserIdAsync(string userId);
    Task<UserSubscription> CreatePendingAsync(string userId, string planId, string paykuClientId);
    Task<UserSubscription?> ActivateAsync(string paykuSubscriptionId, string paykuClientId, DateTime nextBillingDate);
    Task<UserSubscription?> CancelAsync(string userId);
    Task RecordPaymentAsync(string paykuClientId, string paykuSubscriptionId, DateTime nextBillingDate);
}
