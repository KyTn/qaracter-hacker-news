using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsItemContractTests
{
    [Fact]
    public async Task Maps_complete_story_without_wire_types()
    {
        const string json = """
            {"id":42,"type":"story","title":"A title","url":"https://example.test/","by":"alice","time":123,"score":99,"descendants":7,"deleted":false,"dead":false}
            """;
        ScriptedHttpMessageHandler handler = new((request, _, _) =>
        {
            Assert.Equal("/v0/item/42.json", request.RequestUri!.AbsolutePath);
            return Task.FromResult(ScriptedHttpMessageHandler.Json(json));
        });
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<HackerNewsItem> result = await fixture.Client.GetItemAsync(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value!.Id);
        Assert.Equal("A title", result.Value.Title);
        Assert.Equal(99, result.Value.Score);
        Assert.Equal(7, result.Value.Descendants);
    }

    [Fact]
    public async Task Preserves_absent_optional_fields()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json("{\"id\":42,\"type\":\"story\"}")));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<HackerNewsItem> result = await fixture.Client.GetItemAsync(42);

        Assert.Null(result.Value!.Title);
        Assert.Null(result.Value.Score);
        Assert.Null(result.Value.Descendants);
    }
}
