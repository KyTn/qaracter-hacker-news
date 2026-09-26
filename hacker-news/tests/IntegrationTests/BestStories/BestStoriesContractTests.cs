using System.Net;
using System.Text.Json;
using Application.HackerNews;
using IntegrationTests.BestStories.Support;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesContractTests
{
    [Fact]
    public async Task Endpoint_returns_exact_public_contract_in_score_order()
    {
        await using BestStoriesApiFactory factory = new();
        factory.HackerNews.FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1, 2]);
        factory.HackerNews.ItemResults[1] = Success(Item(1, 10, null));
        factory.HackerNews.ItemResults[2] = Success(Item(2, 20, "https://example.test/2"));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/best-stories?n=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(2, body.GetArrayLength());
        JsonElement first = body[0];
        Assert.Equal(new[] { "title", "uri", "postedBy", "time", "score", "commentCount" },
            first.EnumerateObject().Select(property => property.Name));
        Assert.Equal(20, first.GetProperty("score").GetInt64());
        Assert.Equal(JsonValueKind.Null, body[1].GetProperty("uri").ValueKind);
        Assert.EndsWith("+00:00", first.GetProperty("time").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_feed_returns_empty_array()
    {
        await using BestStoriesApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/best-stories?n=1");
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, body.GetArrayLength());
    }

    private static HackerNewsItem Item(long id, long score, string? url) =>
        new(id, "story", $"Story {id}", url, "author", 1_700_000_000, score, 4, false, false);

    private static HackerNewsResult<HackerNewsItem> Success(HackerNewsItem item) =>
        HackerNewsResult<HackerNewsItem>.Success(item);
}
