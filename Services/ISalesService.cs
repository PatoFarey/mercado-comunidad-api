using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface ISalesService
{
    Task<SaleResponse> CreateGuestSaleAsync(CreateGuestSaleRequest request);
    Task<PaginatedResult<SaleResponse>> GetPaginatedAsync(int pageNumber, int pageSize, string? status = null, string? storeId = null, DateTime? dateFrom = null, DateTime? dateTo = null, IEnumerable<string>? allowedStoreIds = null, bool includeAllStores = false);
    Task<SaleResponse?> GetByIdAsync(string id);
    Task<SaleResponse?> UpdateStatusAsync(string id, string status, string? storeObservation = null);
    Task<SaleResponse?> UpdateSaleAsync(string id, UpdateSaleRequest request);
}
