using System.Net;
using IntegrationTests.BestStories.Support;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesCoalescingTests
{
    [Fact]
    public async Task Concurrent_cold_endpoint_requests_share_feed_and_item_populations()
    {
        ControlledUpstreamHandler upstream = new(async (request, cancellationToken) =>
        {
            await Task.Delay(20, cancellationToken);
            string path = request.RequestUri!.AbsolutePath;
            return path switch
            {
                "/v0/beststories.json" => ControlledUpstreamHandler.Json("[1,2]"),
                "/v0/item/1.json" => ControlledUpstreamHandler.Json(Story(1, 10)),
                "/v0/item/2.json" => ControlledUpstreamHandler.Json(Story(2, 20)),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        await using CachedBestStoriesApiFactory factory = new(upstream);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 100)
                .Select(_ => client.GetAsync("/api/v1/best-stories?n=2")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(3, upstream.RequestCount);
        Assert.InRange(upstream.MaximumActive, 1, 16);

        HttpResponseMessage cached = await client.GetAsync("/api/v1/best-stories?n=1");
        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.Equal(3, upstream.RequestCount);
    }

    private static string Story(long id, long score) =>
        $$"""{"id":{{id}},"type":"story","title":"Story {{id}}","url":"https://example.test/{{id}}","by":"author","time":1700000000,"score":{{score}},"descendants":1}""";
}
