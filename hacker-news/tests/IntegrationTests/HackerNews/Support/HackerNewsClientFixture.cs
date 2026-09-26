using Application.HackerNews;
using Infrastructure.HackerNews;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.HackerNews.Support;

internal sealed class HackerNewsClientFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public HackerNewsClientFixture(
        ScriptedHttpMessageHandler handler,
        Action<HackerNewsOptions>? configure = null,
        ILogger<HackerNewsClient>? logger = null)
    {
        Options = new HackerNewsOptions();
        configure?.Invoke(Options);

        ServiceCollection services = new();
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = Options.MaximumPayloadBytes;
            options.MaximumKeyLength = Options.MaximumCacheKeyLength;
        });
        _provider = services.BuildServiceProvider();

        HttpClient httpClient = new(handler) { BaseAddress = Options.BaseAddress };
        Client = new HackerNewsClient(
            httpClient,
            _provider.GetRequiredService<HybridCache>(),
            new HackerNewsConcurrencyGate(Options),
            Microsoft.Extensions.Options.Options.Create(Options),
            logger ?? NullLogger<HackerNewsClient>.Instance);
    }

    public HackerNewsOptions Options { get; }
    public IHackerNewsClient Client { get; }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
