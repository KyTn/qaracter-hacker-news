using Infrastructure.HackerNews;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsRegistrationTests
{
    [Fact]
    public void Defaults_are_valid() =>
        Assert.True(new HackerNewsOptionsValidator().Validate(null, new HackerNewsOptions()).Succeeded);

    [Theory]
    [InlineData("http://example.test/")]
    [InlineData("relative")]
    public void Invalid_base_address_is_rejected(string address)
    {
        HackerNewsOptions options = new() { BaseAddress = new Uri(address, UriKind.RelativeOrAbsolute) };
        Assert.True(new HackerNewsOptionsValidator().Validate(null, options).Failed);
    }

    [Fact]
    public void Invalid_timeout_relationship_is_rejected()
    {
        HackerNewsOptions options = new()
        {
            AttemptTimeout = TimeSpan.FromSeconds(5),
            TotalTimeout = TimeSpan.FromSeconds(5)
        };
        Assert.True(new HackerNewsOptionsValidator().Validate(null, options).Failed);
    }

    [Fact]
    public void Invalid_missing_ttl_is_rejected()
    {
        HackerNewsOptions options = new()
        {
            MissingItemCacheTtl = TimeSpan.FromMinutes(6),
            ItemCacheTtl = TimeSpan.FromMinutes(5)
        };
        Assert.True(new HackerNewsOptionsValidator().Validate(null, options).Failed);
    }
}
