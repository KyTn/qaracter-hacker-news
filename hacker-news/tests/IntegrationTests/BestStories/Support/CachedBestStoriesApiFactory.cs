using Application.HackerNews;
using Infrastructure.HackerNews;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.BestStories.Support;

internal sealed class CachedBestStoriesApiFactory(ControlledUpstreamHandler handler)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHackerNewsClient>();
            services.AddSingleton<IHackerNewsClient>(provider =>
            {
                HackerNewsOptions options = provider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
                HttpClient httpClient = new(handler) { BaseAddress = options.BaseAddress };
                return new HackerNewsClient(
                    httpClient,
                    provider.GetRequiredService<HybridCache>(),
                    provider.GetRequiredService<HackerNewsConcurrencyGate>(),
                    provider.GetRequiredService<IOptions<HackerNewsOptions>>(),
                    provider.GetRequiredService<ILogger<HackerNewsClient>>());
            });
        });
    }
}
