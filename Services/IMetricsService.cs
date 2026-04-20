using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface IMetricsService
{
    Task TrackEventAsync(TrackMetricRequest request);
    Task TrackBatchAsync(IEnumerable<TrackMetricRequest> requests);
    Task<MetricsSummaryResponse> GetSummaryAsync(
        string? storeId = null,
        string? productId = null,
        string? communityId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        IEnumerable<string>? allowedStoreIds = null,
        bool includeAllStores = false);
}
