using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using ApiMercadoComunidad.Configuration;

namespace ApiMercadoComunidad.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task<(bool success, string? errorMessage)> SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);
            smtpClient.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);
            smtpClient.EnableSsl = _emailSettings.EnableSsl;

            await smtpClient.SendMailAsync(message);

            _logger.LogInformation("Email enviado exitosamente a {Email}", to);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar email a {Email}", to);
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? errorMessage)> SendWelcomeEmailAsync(string to, string userName, string userId)
    {
        var subject = "¡Bienvenido a Mercado Comunidad!";
        var htmlBody = GetWelcomeEmailTemplate(userName, userId);

        return await SendEmailAsync(to, subject, htmlBody);
    }

    private string GetWelcomeEmailTemplate(string userName, string userId)
    {
        var verificationLink = $"https://mercadocomunidad.cl/verificarmail/{userId}";

        return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Bienvenido a Mercado Comunidad</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 10px;'>
        <h1 style='color: #007bff; text-align: center;'>¡Bienvenido a Mercado Comunidad!</h1>
        
        <p style='font-size: 16px;'>Hola <strong>{userName}</strong>,</p>
        
        <p style='font-size: 16px;'>
            Estamos muy felices de tenerte con nosotros. Tu cuenta ha sido creada exitosamente.
        </p>
        
        <div style='background-color: #28a745; color: white; padding: 15px; border-radius: 5px; text-align: center; margin: 30px 0;'>
            <a href='{verificationLink}' style='color: white; text-decoration: none; font-size: 18px; font-weight: bold;'>
                ✓ VERIFICAR E-MAIL
            </a>
        </div>
        
        <p style='font-size: 14px; color: #666; text-align: center;'>
            O copia y pega este enlace en tu navegador:<br>
            <a href='{verificationLink}' style='color: #007bff; word-break: break-all;'>{verificationLink}</a>
        </p>
        
        <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
        
        <p>En Mercado Comunidad, podrás:</p>
        <ul>
            <li><strong>Tu Tienda:</strong> Crear tu tienda y publicar productos.</li>   
            <li><strong>PRONTO !! Tu Comunidad:</strong> Crear tu comunidad e invitar a otros a unirse y vender.</li>
            <li><strong>Crecer:</strong> Conectando comunidades, creando oportunidades.</li>
        </ul>
        
        <p style='font-size: 14px; color: #666;'>
            Si tienes alguna pregunta, no dudes en contactarnos.
        </p>
        
        <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
        
        <p style='font-size: 12px; color: #999; text-align: center;'>
            Mercado Comunidad<br>
            contacto@mercadocomunidad.cl<br>
            &copy; 2026 Todos los derechos reservados
        </p>
    </div>
</body>
</html>";
    }
}