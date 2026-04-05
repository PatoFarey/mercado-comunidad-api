using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ApiMercadoComunidad.Services;

public class MetricsService : IMetricsService
{
    private readonly IMongoCollection<MetricEvent> _metricsCollection;

    public MetricsService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _metricsCollection = mongoDatabase.GetCollection<MetricEvent>("metric_events");
    }

    public async Task TrackEventAsync(TrackMetricRequest request)
    {
        var normalizedType = NormalizeEventType(request.EventType);

        var metricEvent = new MetricEvent
        {
            EventType = normalizedType,
            ProductId = NormalizeNullable(request.ProductId),
            StoreId = NormalizeNullable(request.StoreId),
            CommunityId = NormalizeNullable(request.CommunityId),
            ProductTitle = request.ProductTitle?.Trim() ?? string.Empty,
            StoreName = request.StoreName?.Trim() ?? string.Empty,
            StoreSlug = request.StoreSlug?.Trim() ?? string.Empty,
            Source = request.Source?.Trim() ?? string.Empty,
            SessionId = request.SessionId?.Trim() ?? string.Empty,
            Path = request.Path?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _metricsCollection.InsertOneAsync(metricEvent);
    }

    public async Task TrackBatchAsync(IEnumerable<TrackMetricRequest> requests)
    {
        var events = new List<MetricEvent>();

        foreach (var request in requests)
        {
            var normalizedType = NormalizeEventType(request.EventType);
            events.Add(new MetricEvent
            {
                EventType = normalizedType,
                ProductId = NormalizeNullable(request.ProductId),
                StoreId = NormalizeNullable(request.StoreId),
                CommunityId = NormalizeNullable(request.CommunityId),
                ProductTitle = request.ProductTitle?.Trim() ?? string.Empty,
                StoreName = request.StoreName?.Trim() ?? string.Empty,
                StoreSlug = request.StoreSlug?.Trim() ?? string.Empty,
                Source = request.Source?.Trim() ?? string.Empty,
                SessionId = request.SessionId?.Trim() ?? string.Empty,
                Path = request.Path?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (events.Count == 0)
            return;

        await _metricsCollection.InsertManyAsync(events);
    }

    public async Task<MetricsSummaryResponse> GetSummaryAsync(
        string? storeId = null,
        string? communityId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        IEnumerable<string>? allowedStoreIds = null,
        bool includeAllStores = false)
    {
        var filters = new List<FilterDefinition<MetricEvent>>();

        if (!string.IsNullOrWhiteSpace(storeId))
        {
            filters.Add(Builders<MetricEvent>.Filter.Eq(m => m.StoreId, storeId));
        }
        else if (!includeAllStores)
        {
            var allowedIds = allowedStoreIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? new List<string>();
            if (allowedIds.Count == 0)
            {
                return new MetricsSummaryResponse();
            }

            filters.Add(Builders<MetricEvent>.Filter.In(m => m.StoreId, allowedIds));
        }

        if (!string.IsNullOrWhiteSpace(communityId))
            filters.Add(Builders<MetricEvent>.Filter.Eq(m => m.CommunityId, communityId));

        if (dateFrom.HasValue)
            filters.Add(Builders<MetricEvent>.Filter.Gte(m => m.CreatedAt, dateFrom.Value));

        if (dateTo.HasValue)
            filters.Add(Builders<MetricEvent>.Filter.Lt(m => m.CreatedAt, dateTo.Value));

        var filter = filters.Count > 0
            ? Builders<MetricEvent>.Filter.And(filters)
            : Builders<MetricEvent>.Filter.Empty;

        var events = await _metricsCollection
            .Find(filter)
            .Project(m => new
            {
                m.EventType,
                m.CreatedAt,
                m.ProductId,
                m.ProductTitle,
                m.StoreId,
                m.StoreName
            })
            .ToListAsync();

        var counts = new MetricsCountsResponse
        {
            TotalEvents = events.LongCount(),
            Impressions = events.LongCount(e => e.EventType.Equals(MetricEventTypes.Impression, StringComparison.OrdinalIgnoreCase)),
            ProductViews = events.LongCount(e => e.EventType.Equals(MetricEventTypes.ProductView, StringComparison.OrdinalIgnoreCase)),
            StoreViews = events.LongCount(e => e.EventType.Equals(MetricEventTypes.StoreView, StringComparison.OrdinalIgnoreCase)),
            CommunityViews = events.LongCount(e => e.EventType.Equals(MetricEventTypes.CommunityView, StringComparison.OrdinalIgnoreCase)),
        };

        var timeline = events
            .GroupBy(e => e.CreatedAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => new MetricsTimelinePointResponse
            {
                Date = group.Key,
                TotalEvents = group.LongCount(),
                Impressions = group.LongCount(e => e.EventType.Equals(MetricEventTypes.Impression, StringComparison.OrdinalIgnoreCase)),
                ProductViews = group.LongCount(e => e.EventType.Equals(MetricEventTypes.ProductView, StringComparison.OrdinalIgnoreCase)),
                StoreViews = group.LongCount(e => e.EventType.Equals(MetricEventTypes.StoreView, StringComparison.OrdinalIgnoreCase)),
                CommunityViews = group.LongCount(e => e.EventType.Equals(MetricEventTypes.CommunityView, StringComparison.OrdinalIgnoreCase)),
            })
            .ToList();

        var topProducts = events
            .Where(e => !string.IsNullOrWhiteSpace(e.ProductId))
            .GroupBy(e => new
            {
                e.ProductId,
                e.ProductTitle,
                e.StoreId,
                e.StoreName
            })
            .Select(group => new TopProductMetricResponse
            {
                ProductId = group.Key.ProductId,
                ProductTitle = group.Key.ProductTitle,
                StoreId = group.Key.StoreId,
                StoreName = group.Key.StoreName,
                Impressions = group.LongCount(e => e.EventType.Equals(MetricEventTypes.Impression, StringComparison.OrdinalIgnoreCase)),
                Views = group.LongCount(e => e.EventType.Equals(MetricEventTypes.ProductView, StringComparison.OrdinalIgnoreCase)),
            })
            .OrderByDescending(item => item.Views)
            .ThenByDescending(item => item.Impressions)
            .Take(10)
            .ToList();

        return new MetricsSummaryResponse
        {
            Counts = counts,
            Timeline = timeline,
            TopProducts = topProducts
        };
    }

    private static string NormalizeEventType(string eventType)
    {
        var normalized = MetricEventTypes.All.FirstOrDefault(item =>
            item.Equals(eventType?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (normalized == null)
            throw new InvalidOperationException("Tipo de evento de métrica inválido.");

        return normalized;
    }

    private static string? NormalizeNullable(string value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
