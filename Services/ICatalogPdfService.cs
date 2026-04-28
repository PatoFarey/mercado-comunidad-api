namespace ApiMercadoComunidad.Services;

public interface ICatalogPdfService
{
    Task<byte[]> GenerateStoreCatalogAsync(string storeId);
}
