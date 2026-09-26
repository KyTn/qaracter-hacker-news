namespace Infrastructure.HackerNews;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    public Uri BaseAddress { get; set; } = new("https://hacker-news.firebaseio.com/");
    public TimeSpan FeedCacheTtl { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ItemCacheTtl { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan MissingItemCacheTtl { get; set; } = TimeSpan.FromSeconds(30);
    public int MaximumPayloadBytes { get; set; } = 1_048_576;
    public int MaximumCacheKeyLength { get; set; } = 128;
    public int ConcurrencyPermitLimit { get; set; } = 16;
    public int ConcurrencyQueueLimit { get; set; } = 64;
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public int RetryCount { get; set; } = 2;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}
