using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class BestStoryFeedCacheTests
{
    [Fact]
    public async Task Fresh_entry_avoids_upstream_call()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("[1,2]")));
        await using HackerNewsClientFixture fixture = new(handler);

        await fixture.Client.GetBestStoryIdsAsync();
        await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Concurrent_cold_callers_share_one_population()
    {
        ScriptedHttpMessageHandler handler = new(async (_, _, token) =>
        {
            await Task.Delay(50, token);
            return ScriptedHttpMessageHandler.Json("[1,2]");
        });
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>>[] results = await Task.WhenAll(
            Enumerable.Range(0, 100).Select(_ => fixture.Client.GetBestStoryIdsAsync().AsTask()));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Expired_feed_is_reloaded()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("[1]")));
        await using HackerNewsClientFixture fixture = new(handler, options =>
            options.FeedCacheTtl = TimeSpan.FromMilliseconds(20));

        await fixture.Client.GetBestStoryIdsAsync();
        await Task.Delay(60);
        await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(2, handler.RequestCount);
    }
}
