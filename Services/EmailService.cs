using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models.DTOs;

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

    public async Task<(bool success, string? errorMessage)> SendPasswordResetCodeAsync(string to, string userName, string resetCode)
    {
        var subject = "Recuperación de contraseña - Mercado Comunidad";
        var htmlBody = GetPasswordResetEmailTemplate(userName, resetCode);

        return await SendEmailAsync(to, subject, htmlBody);
    }

    public async Task<(bool success, string? errorMessage)> SendOrderConfirmationToBuyerAsync(string to, SaleResponse sale, string storeEmail = "")
    {
        var subject = $"Pedido recibido - {sale.StoreName} | Mercado Comunidad";
        var htmlBody = GetOrderConfirmationBuyerTemplate(sale, storeEmail);
        return await SendEmailAsync(to, subject, htmlBody);
    }

    public async Task<(bool success, string? errorMessage)> SendOrderNotificationToSellerAsync(string to, string storeName, SaleResponse sale)
    {
        var subject = $"Nuevo pedido recibido en {storeName} | Mercado Comunidad";
        var htmlBody = GetOrderNotificationSellerTemplate(sale);
        return await SendEmailAsync(to, subject, htmlBody);
    }

    public async Task<(bool success, string? errorMessage)> SendCommunityRequestToAdminAsync(string to, string adminName, string storeName, string communityName, string message)
    {
        var subject = $"Nueva solicitud de publicación en {communityName} | Mercado Comunidad";
        var htmlBody = GetCommunityRequestAdminTemplate(adminName, storeName, communityName, message);
        return await SendEmailAsync(to, subject, htmlBody);
    }

    public async Task<(bool success, string? errorMessage)> SendCommunityRequestResultToStoreAsync(string to, string storeName, string communityName, bool approved, string reason)
    {
        var subject = approved
            ? $"Solicitud aprobada: {communityName} | Mercado Comunidad"
            : $"Solicitud rechazada: {communityName} | Mercado Comunidad";
        var htmlBody = GetCommunityRequestResultTemplate(storeName, communityName, approved, reason);
        return await SendEmailAsync(to, subject, htmlBody);
    }

    private string GetCommunityRequestAdminTemplate(string adminName, string storeName, string communityName, string message)
    {
        var messageBlock = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : $@"<div style='background:#fffbeb;border-left:4px solid #f59e0b;padding:12px 16px;border-radius:4px;margin:20px 0;'>
                   <p style='margin:0 0 4px;font-size:13px;font-weight:700;color:#92400e;'>Mensaje del solicitante:</p>
                   <p style='margin:0;font-size:14px;color:#78350f;'>{message}</p>
                 </div>";

        return $@"
<!DOCTYPE html>
<html lang='es'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='font-family:Arial,sans-serif;line-height:1.6;color:#333;max-width:600px;margin:0 auto;padding:20px;'>
  <div style='background:#f8f9fa;padding:30px;border-radius:10px;'>
    <h1 style='color:#f97316;text-align:center;margin-top:0;'>Nueva solicitud de publicación</h1>
    <p style='font-size:16px;'>Hola <strong>{adminName}</strong>,</p>
    <p style='font-size:15px;'>La tienda <strong>{storeName}</strong> ha solicitado publicarse en tu comunidad <strong>{communityName}</strong>.</p>
    {messageBlock}
    <div style='background:#fff;border-radius:8px;padding:16px;margin:20px 0;border:1px solid #e5e7eb;text-align:center;'>
      <p style='margin:0 0 12px;font-size:14px;color:#374151;'>Ingresa a tu panel de administración para aprobar o rechazar la solicitud.</p>
      <a href='https://mercadocomunidad.cl/admin/mis-comunidades' style='display:inline-block;background:#f97316;color:#fff;text-decoration:none;padding:10px 24px;border-radius:6px;font-weight:700;font-size:14px;'>Ver solicitudes</a>
    </div>
    <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'>
    <p style='font-size:12px;color:#999;text-align:center;'>Mercado Comunidad · contacto@mercadocomunidad.cl · &copy; 2026</p>
  </div>
</body>
</html>";
    }

    private string GetCommunityRequestResultTemplate(string storeName, string communityName, bool approved, string reason)
    {
        var color = approved ? "#16a34a" : "#dc2626";
        var title = approved ? "Solicitud aprobada" : "Solicitud rechazada";
        var message = approved
            ? $"Tu tienda <strong>{storeName}</strong> fue aprobada para publicarse en <strong>{communityName}</strong>. Ya puedes ir a tu panel y publicar tus productos."
            : $"Tu solicitud para publicar <strong>{storeName}</strong> en <strong>{communityName}</strong> fue rechazada.";

        var reasonBlock = (!approved && !string.IsNullOrWhiteSpace(reason))
            ? $@"<div style='background:#fef2f2;border-left:4px solid #dc2626;padding:12px 16px;border-radius:4px;margin:20px 0;'>
                   <p style='margin:0 0 4px;font-size:13px;font-weight:700;color:#991b1b;'>Motivo:</p>
                   <p style='margin:0;font-size:14px;color:#7f1d1d;'>{reason}</p>
                 </div>"
            : string.Empty;

        var actionBlock = approved
            ? $@"<div style='text-align:center;margin:20px 0;'>
                   <a href='https://mercadocomunidad.cl/admin/comunidades' style='display:inline-block;background:#2563eb;color:#fff;text-decoration:none;padding:10px 24px;border-radius:6px;font-weight:700;font-size:14px;'>Ir a mis comunidades</a>
                 </div>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html lang='es'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='font-family:Arial,sans-serif;line-height:1.6;color:#333;max-width:600px;margin:0 auto;padding:20px;'>
  <div style='background:#f8f9fa;padding:30px;border-radius:10px;'>
    <h1 style='color:{color};text-align:center;margin-top:0;'>{title}</h1>
    <p style='font-size:15px;'>{message}</p>
    {reasonBlock}
    {actionBlock}
    <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'>
    <p style='font-size:12px;color:#999;text-align:center;'>Mercado Comunidad · contacto@mercadocomunidad.cl · &copy; 2026</p>
  </div>
</body>
</html>";
    }

    private string BuildItemsTable(SaleResponse sale)
    {
        var sb = new StringBuilder();
        sb.Append("<table style='width:100%;border-collapse:collapse;margin:16px 0;'>");
        sb.Append("<thead><tr style='background:#f3f4f6;'>");
        sb.Append("<th style='text-align:left;padding:8px 12px;font-size:13px;color:#374151;'>Producto</th>");
        sb.Append("<th style='text-align:center;padding:8px 12px;font-size:13px;color:#374151;'>Cant.</th>");
        sb.Append("<th style='text-align:right;padding:8px 12px;font-size:13px;color:#374151;'>Precio</th>");
        sb.Append("<th style='text-align:right;padding:8px 12px;font-size:13px;color:#374151;'>Subtotal</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var item in sale.Items)
        {
            var unitPrice = item.UnitPrice.ToString("N0");
            var lineTotal = item.LineTotal.ToString("N0");
            sb.Append($"<tr style='border-bottom:1px solid #e5e7eb;'>");
            sb.Append($"<td style='padding:10px 12px;font-size:14px;color:#111827;'>{item.ProductTitle}</td>");
            sb.Append($"<td style='padding:10px 12px;font-size:14px;text-align:center;color:#374151;'>{item.Quantity}</td>");
            sb.Append($"<td style='padding:10px 12px;font-size:14px;text-align:right;color:#374151;'>${unitPrice}</td>");
            sb.Append($"<td style='padding:10px 12px;font-size:14px;text-align:right;font-weight:600;color:#111827;'>${lineTotal}</td>");
            sb.Append("</tr>");
        }
        sb.Append($"<tr style='background:#f3f4f6;'>");
        sb.Append($"<td colspan='3' style='padding:10px 12px;font-size:14px;font-weight:700;color:#111827;text-align:right;'>Total</td>");
        sb.Append($"<td style='padding:10px 12px;font-size:16px;font-weight:700;color:#2563eb;text-align:right;'>${sale.Total.ToString("N0")}</td>");
        sb.Append("</tr></tbody></table>");
        return sb.ToString();
    }

    private string GetOrderConfirmationBuyerTemplate(SaleResponse sale, string storeEmail = "")
    {
        var itemsTable = BuildItemsTable(sale);
        var notes = string.IsNullOrWhiteSpace(sale.Notes) ? "—" : sale.Notes;
        var storeContact = string.IsNullOrWhiteSpace(storeEmail)
            ? sale.StoreName
            : $"{sale.StoreName} · {storeEmail}";

        return $@"
<!DOCTYPE html>
<html lang='es'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='font-family:Arial,sans-serif;line-height:1.6;color:#333;max-width:600px;margin:0 auto;padding:20px;'>
  <div style='background:#f8f9fa;padding:30px;border-radius:10px;'>
    <h1 style='color:#2563eb;text-align:center;margin-top:0;'>Pedido recibido</h1>
    <p style='font-size:16px;'>Hola <strong>{sale.CustomerName}</strong>,</p>
    <p style='font-size:15px;'>Tu pedido en <strong>{sale.StoreName}</strong> fue registrado correctamente. El vendedor se pondrá en contacto contigo para coordinar la entrega y el pago.</p>

    <div style='background:#fff;border-radius:8px;padding:16px;margin:20px 0;border:1px solid #e5e7eb;'>
      <h3 style='margin-top:0;color:#111827;'>Resumen del pedido</h3>
      {itemsTable}
    </div>

    <div style='background:#fff;border-radius:8px;padding:16px;margin:20px 0;border:1px solid #e5e7eb;'>
      <h3 style='margin-top:0;color:#111827;'>Tus datos de entrega</h3>
      <p style='margin:4px 0;font-size:14px;'><strong>Nombre:</strong> {sale.CustomerName}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Email:</strong> {sale.CustomerEmail}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Teléfono:</strong> {sale.CustomerPhone}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Dirección:</strong> {sale.CustomerAddress}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Notas:</strong> {notes}</p>
    </div>

    <div style='background:#eff6ff;border-left:4px solid #2563eb;padding:12px 16px;border-radius:4px;margin:20px 0;'>
      <p style='margin:0;font-size:14px;color:#1d4ed8;'><strong>Pago contra entrega o coordinación directa con el vendedor.</strong><br>No se realizó ningún cobro en línea.</p>
    </div>

    <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'>
    <p style='font-size:12px;color:#999;text-align:center;'>{storeContact} · &copy; 2026<br>Enviado por Mercado Comunidad</p>
  </div>
</body>
</html>";
    }

    private string GetOrderNotificationSellerTemplate(SaleResponse sale)
    {
        var itemsTable = BuildItemsTable(sale);
        var notes = string.IsNullOrWhiteSpace(sale.Notes) ? "—" : sale.Notes;

        return $@"
<!DOCTYPE html>
<html lang='es'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='font-family:Arial,sans-serif;line-height:1.6;color:#333;max-width:600px;margin:0 auto;padding:20px;'>
  <div style='background:#f8f9fa;padding:30px;border-radius:10px;'>
    <h1 style='color:#059669;text-align:center;margin-top:0;'>Nuevo pedido recibido</h1>
    <p style='font-size:16px;'>Tienes un nuevo pedido en <strong>{sale.StoreName}</strong>. Contacta al comprador para coordinar la entrega.</p>

    <div style='background:#fff;border-radius:8px;padding:16px;margin:20px 0;border:1px solid #e5e7eb;'>
      <h3 style='margin-top:0;color:#111827;'>Datos del comprador</h3>
      <p style='margin:4px 0;font-size:14px;'><strong>Nombre:</strong> {sale.CustomerName}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Email:</strong> {sale.CustomerEmail}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Teléfono:</strong> {sale.CustomerPhone}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Dirección:</strong> {sale.CustomerAddress}</p>
      <p style='margin:4px 0;font-size:14px;'><strong>Notas:</strong> {notes}</p>
    </div>

    <div style='background:#fff;border-radius:8px;padding:16px;margin:20px 0;border:1px solid #e5e7eb;'>
      <h3 style='margin-top:0;color:#111827;'>Productos pedidos</h3>
      {itemsTable}
    </div>

    <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'>
    <p style='font-size:12px;color:#999;text-align:center;'>Mercado Comunidad · contacto@mercadocomunidad.cl · &copy; 2026</p>
  </div>
</body>
</html>";
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

    private string GetPasswordResetEmailTemplate(string userName, string resetCode)
    {
        return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Recuperación de Contraseña</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 10px;'>
        <h1 style='color: #dc3545; text-align: center;'>🔐 Recuperación de Contraseña</h1>
        
        <p style='font-size: 16px;'>Hola <strong>{userName}</strong>,</p>
        
        <p style='font-size: 16px;'>
            Hemos recibido una solicitud para restablecer la contraseña de tu cuenta en Mercado Comunidad.
        </p>
        
        <div style='background-color: #fff; border: 3px dashed #007bff; padding: 20px; border-radius: 10px; text-align: center; margin: 30px 0;'>
            <p style='font-size: 14px; color: #666; margin-bottom: 10px;'>Tu código de verificación es:</p>
            <h2 style='font-size: 48px; font-weight: bold; color: #007bff; letter-spacing: 8px; margin: 10px 0;'>{resetCode}</h2>
            <p style='font-size: 12px; color: #999; margin-top: 10px;'>Este código expira en 30 minutos</p>
        </div>
        
        <div style='background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0;'>
            <p style='margin: 0; font-size: 14px;'>
                <strong>⚠️ Importante:</strong> Si no solicitaste restablecer tu contraseña, ignora este correo. 
                Tu cuenta permanecerá segura.
            </p>
        </div>
        
        <p style='font-size: 14px; color: #666;'>
            Para restablecer tu contraseña, ingresa este código en la página de recuperación junto con tu nueva contraseña.
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