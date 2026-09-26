using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsCancellationTests
{
    [Fact]
    public async Task Resilience_attempt_timeout_is_classified()
    {
        ScriptedHttpMessageHandler handler = new(async (_, _, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return ScriptedHttpMessageHandler.Json("[]");
        });
        await using ResilientClientFixture fixture = new(handler, new Dictionary<string, string?>
        {
            ["HackerNews:AttemptTimeout"] = "00:00:00.050",
            ["HackerNews:TotalTimeout"] = "00:00:00.250",
            ["HackerNews:RetryCount"] = "0"
        });

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.Timeout, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Caller_cancellation_is_distinct()
    {
        ScriptedHttpMessageHandler handler = new(async (_, _, token) =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1), token);
            return ScriptedHttpMessageHandler.Json("[]");
        });
        await using HackerNewsClientFixture fixture = new(handler);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(30));

        HackerNewsResult<IReadOnlyList<long>> result =
            await fixture.Client.GetBestStoryIdsAsync(cancellation.Token);

        Assert.Equal(HackerNewsOutcome.Canceled, result.Outcome);
    }

    [Fact]
    public async Task Canceling_one_waiter_does_not_cancel_shared_population()
    {
        ScriptedHttpMessageHandler handler = new(async (_, _, token) =>
        {
            await Task.Delay(80, token);
            return ScriptedHttpMessageHandler.Json("[1]");
        });
        await using HackerNewsClientFixture fixture = new(handler);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(20));

        Task<HackerNewsResult<IReadOnlyList<long>>> canceled =
            fixture.Client.GetBestStoryIdsAsync(cancellation.Token).AsTask();
        Task<HackerNewsResult<IReadOnlyList<long>>> remaining =
            fixture.Client.GetBestStoryIdsAsync().AsTask();

        Assert.Equal(HackerNewsOutcome.Canceled, (await canceled).Outcome);
        Assert.True((await remaining).IsSuccess);
        Assert.Equal(1, handler.RequestCount);
    }
}
