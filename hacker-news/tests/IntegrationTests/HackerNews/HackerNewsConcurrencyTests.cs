using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsConcurrencyTests
{
    [Fact]
    public async Task Global_gate_bounds_one_hundred_distinct_item_requests_and_rejects_excess_queue()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedHttpMessageHandler handler = new(async (request, _, token) =>
        {
            await release.Task.WaitAsync(token);
            long id = long.Parse(request.RequestUri!.Segments[^1].Replace(".json", "", StringComparison.Ordinal));
            return ScriptedHttpMessageHandler.Json($"{{\"id\":{id}}}");
        });
        await using HackerNewsClientFixture fixture = new(handler, options =>
        {
            options.ConcurrencyPermitLimit = 4;
            options.ConcurrencyQueueLimit = 8;
        });

        Task<HackerNewsResult<HackerNewsItem>>[] requests = Enumerable.Range(1, 100)
            .Select(id => fixture.Client.GetItemAsync(id).AsTask())
            .ToArray();
        await Task.Delay(80);
        release.SetResult();
        HackerNewsResult<HackerNewsItem>[] results = await Task.WhenAll(requests);

        Assert.Contains(results, result => result.Outcome == HackerNewsOutcome.Throttled);
        Assert.True(handler.MaximumActive <= 4);
        Assert.True(handler.RequestCount <= 12);
    }
}
