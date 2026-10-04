using Prometheus;

namespace HappyHeadlines.Shared;

public static class CacheMetrics
{
    public const string Article = "article";
    public const string Comment = "comment";

    private static readonly Counter Lookups = Metrics.CreateCounter(
        "hh_cache_lookups_total", "Cache lookups by cache and result (hit or miss).",
        new CounterConfiguration { LabelNames = ["cache", "result"] });

    private static readonly Histogram LookupDuration = Metrics.CreateHistogram(
        "hh_cache_lookup_duration_seconds", "Time to answer a request, by cache and result. A miss includes the database read.",
        new HistogramConfiguration { LabelNames = ["cache", "result"] });

    public static readonly Counter Evictions = Metrics.CreateCounter(
        "hh_cache_evictions_total", "Entries the cache evicted to stay within its size limit.",
        new CounterConfiguration { LabelNames = ["cache"] });

    public static readonly Gauge Entries = Metrics.CreateGauge(
        "hh_cache_entries", "Entries in the cache, as last seen by this instance.",
        new GaugeConfiguration { LabelNames = ["cache"] });

    public static void Record(string cache, bool hit, TimeSpan elapsed)
    {
        var result = hit ? "hit" : "miss";
        Lookups.WithLabels(cache, result).Inc();
        LookupDuration.WithLabels(cache, result).Observe(elapsed.TotalSeconds);
    }
}
