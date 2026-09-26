using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsItemValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_id_does_not_call_upstream(long id)
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            throw new InvalidOperationException("Should not be called."));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<HackerNewsItem> result = await fixture.Client.GetItemAsync(id);

        Assert.Equal(HackerNewsOutcome.InvalidInput, result.Outcome);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Mismatched_id_is_invalid_upstream_data()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("{\"id\":43}")));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<HackerNewsItem> result = await fixture.Client.GetItemAsync(42);

        Assert.Equal(HackerNewsOutcome.InvalidUpstreamData, result.Outcome);
    }
}
