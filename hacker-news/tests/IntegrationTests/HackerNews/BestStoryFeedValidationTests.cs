using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class BestStoryFeedValidationTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[0]")]
    [InlineData("[-1]")]
    [InlineData("[9223372036854775808]")]
    [InlineData("not-json")]
    public async Task Invalid_payload_is_classified(string payload)
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json(payload)));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.InvalidUpstreamData, result.Outcome);
    }

    [Fact]
    public async Task Oversized_payload_is_rejected()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("[123456789]")));
        await using HackerNewsClientFixture fixture = new(handler, options =>
            options.MaximumPayloadBytes = 5);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.InvalidUpstreamData, result.Outcome);
    }
}
