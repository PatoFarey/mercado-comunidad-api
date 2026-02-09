namespace ApiMercadoComunidad.Services;

public interface IEmailService
{
    Task<(bool success, string? errorMessage)> SendEmailAsync(string to, string subject, string htmlBody);
    Task<(bool success, string? errorMessage)> SendWelcomeEmailAsync(string to, string userName, string userId);
    Task<(bool success, string? errorMessage)> SendPasswordResetCodeAsync(string to, string userName, string resetCode);
}