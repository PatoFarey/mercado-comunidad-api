namespace ApiMercadoComunidad.Models.DTOs;

public class TrackMetricRequest
{
    public string EventType { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string CommunityId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string StoreSlug { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class TrackMetricsBatchRequest
{
    public List<TrackMetricRequest> Events { get; set; } = new();
}

public class MetricsCountsResponse
{
    public long TotalEvents { get; set; }
    public long Impressions { get; set; }
    public long ProductViews { get; set; }
    public long StoreViews { get; set; }
    public long CommunityViews { get; set; }
}

public class MetricsTimelinePointResponse
{
    public DateTime Date { get; set; }
    public long TotalEvents { get; set; }
    public long Impressions { get; set; }
    public long ProductViews { get; set; }
    public long StoreViews { get; set; }
    public long CommunityViews { get; set; }
}

public class TopProductMetricResponse
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public long Impressions { get; set; }
    public long Views { get; set; }
}

public class MetricsSummaryResponse
{
    public MetricsCountsResponse Counts { get; set; } = new();
    public List<MetricsTimelinePointResponse> Timeline { get; set; } = new();
    public List<TopProductMetricResponse> TopProducts { get; set; } = new();
}
