using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsRetryTests
{
    [Fact]
    public async Task Retryable_failures_are_bounded_and_can_recover()
    {
        ScriptedHttpMessageHandler handler = new((_, attempt, _) =>
            Task.FromResult(attempt < 3
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : ScriptedHttpMessageHandler.Json("[1]")));
        await using ResilientClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task Permanent_bad_request_is_not_retried()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        await using ResilientClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.Unavailable, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Terminal_http_failure_is_unavailable()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Malformed_success_is_not_unavailable()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("bad")));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.InvalidUpstreamData, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
    }
}
