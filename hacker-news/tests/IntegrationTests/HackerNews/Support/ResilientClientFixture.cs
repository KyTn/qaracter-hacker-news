using Application.HackerNews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.HackerNews.Support;

internal sealed class ResilientClientFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public ResilientClientFixture(
        ScriptedHttpMessageHandler handler,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> settings = new()
        {
            ["HackerNews:BaseAddress"] = "https://hacker-news.test/",
            ["HackerNews:FeedCacheTtl"] = "00:00:30",
            ["HackerNews:ItemCacheTtl"] = "00:05:00",
            ["HackerNews:MissingItemCacheTtl"] = "00:00:30",
            ["HackerNews:MaximumPayloadBytes"] = "1048576",
            ["HackerNews:MaximumCacheKeyLength"] = "128",
            ["HackerNews:ConcurrencyPermitLimit"] = "16",
            ["HackerNews:ConcurrencyQueueLimit"] = "64",
            ["HackerNews:AttemptTimeout"] = "00:00:00.100",
            ["HackerNews:TotalTimeout"] = "00:00:01",
            ["HackerNews:RetryCount"] = "2",
            ["HackerNews:InitialRetryDelay"] = "00:00:00.010",
            ["HackerNews:CircuitBreakerFailureRatio"] = "0.5",
            ["HackerNews:CircuitBreakerMinimumThroughput"] = "10",
            ["HackerNews:CircuitBreakerSamplingDuration"] = "00:00:02",
            ["HackerNews:CircuitBreakerBreakDuration"] = "00:00:01"
        };
        if (overrides is not null)
            foreach ((string key, string? value) in overrides) settings[key] = value;

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHackerNewsIntegration(configuration, () => handler);
        _provider = services.BuildServiceProvider();
        Client = _provider.GetRequiredService<IHackerNewsClient>();
    }

    public IHackerNewsClient Client { get; }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
