using Application.HackerNews;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.BestStories.Support;

internal sealed class BestStoriesApiFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _configuration;

    public BestStoriesApiFactory(IReadOnlyDictionary<string, string?>? configuration = null) =>
        _configuration = configuration ?? new Dictionary<string, string?>();

    public TestHackerNewsClient HackerNews { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach ((string key, string? value) in _configuration)
            builder.UseSetting(key, value);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(_configuration));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHackerNewsClient>();
            services.AddSingleton<IHackerNewsClient>(HackerNews);
        });
    }
}
