using Application.BestStories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesRegistrationTests
{
    [Theory]
    [InlineData("BestStories:ItemConcurrency", "0")]
    [InlineData("BestStories:ItemConcurrency", "17")]
    [InlineData("BestStories:EndpointPermitLimit", "0")]
    [InlineData("BestStories:EndpointQueueLimit", "-1")]
    public void Invalid_configuration_fails_registration(string key, string value)
    {
        ConfigurationManager configuration = new();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value });
        ServiceCollection services = new();

        Assert.Throws<OptionsValidationException>(() =>
            services.AddBestStories(configuration));
    }

    [Fact]
    public void Defaults_register_service_and_options()
    {
        ConfigurationManager configuration = new();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBestStories(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        BestStoriesOptions options = provider.GetRequiredService<BestStoriesOptions>();

        Assert.Equal(8, options.ItemConcurrency);
        Assert.Equal(100, options.EndpointPermitLimit);
        Assert.Equal(200, options.EndpointQueueLimit);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBestStoriesService));
    }
}
