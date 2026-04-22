namespace ApiMercadoComunidad.Configuration;

public class PaykuSettings
{
    public string BaseUrl { get; set; } = "https://testing-apirest.payku.cl";
    public string PublicToken { get; set; } = string.Empty;
    public string PrivateToken { get; set; } = string.Empty;
}
