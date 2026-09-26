using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsItemCacheTests
{
    [Theory]
    [InlineData("null", HttpStatusCode.OK)]
    [InlineData("{}", HttpStatusCode.NotFound)]
    public async Task Missing_item_is_negatively_cached(string body, HttpStatusCode status)
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json(body, status)));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<HackerNewsItem> first = await fixture.Client.GetItemAsync(42);
        HackerNewsResult<HackerNewsItem> second = await fixture.Client.GetItemAsync(42);

        Assert.Equal(HackerNewsOutcome.NotFound, first.Outcome);
        Assert.Equal(HackerNewsOutcome.NotFound, second.Outcome);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Different_item_keys_are_independent()
    {
        ScriptedHttpMessageHandler handler = new((request, _, _) =>
        {
            long id = long.Parse(request.RequestUri!.Segments[^1].Replace(".json", "", StringComparison.Ordinal));
            return Task.FromResult(ScriptedHttpMessageHandler.Json($"{{\"id\":{id}}}"));
        });
        await using HackerNewsClientFixture fixture = new(handler);

        await fixture.Client.GetItemAsync(1);
        await fixture.Client.GetItemAsync(2);
        await fixture.Client.GetItemAsync(1);

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Missing_item_is_reloaded_after_negative_ttl()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("null")));
        await using HackerNewsClientFixture fixture = new(handler, options =>
            options.MissingItemCacheTtl = TimeSpan.FromMilliseconds(20));

        await fixture.Client.GetItemAsync(42);
        await Task.Delay(60);
        await fixture.Client.GetItemAsync(42);

        Assert.Equal(2, handler.RequestCount);
    }
}
