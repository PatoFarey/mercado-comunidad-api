namespace ApiMercadoComunidad.Services;

public interface IEmailService
{
    Task<(bool success, string? errorMessage)> SendEmailAsync(string to, string subject, string htmlBody);
    Task<(bool success, string? errorMessage)> SendWelcomeEmailAsync(string to, string userName, string userId);
    Task<(bool success, string? errorMessage)> SendPasswordResetCodeAsync(string to, string userName, string resetCode);
    Task<(bool success, string? errorMessage)> SendOrderConfirmationToBuyerAsync(string to, Models.DTOs.SaleResponse sale, string storeEmail = "");
    Task<(bool success, string? errorMessage)> SendOrderNotificationToSellerAsync(string to, string storeName, Models.DTOs.SaleResponse sale);
}