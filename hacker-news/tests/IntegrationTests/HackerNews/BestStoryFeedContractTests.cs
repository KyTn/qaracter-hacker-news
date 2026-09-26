using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class BestStoryFeedContractTests
{
    [Fact]
    public async Task Preserves_order_empty_and_duplicates()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("[3,1,3,2]")));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.Success, result.Outcome);
        Assert.Equal([3L, 1L, 3L, 2L], result.Value);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Accepts_empty_feed()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("[]")));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Empty(result.Value!);
    }
}
