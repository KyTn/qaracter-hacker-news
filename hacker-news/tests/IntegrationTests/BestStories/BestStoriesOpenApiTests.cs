using System.Net;
using System.Text.Json;
using IntegrationTests.BestStories.Support;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesOpenApiTests
{
    [Fact]
    public async Task OpenApi_describes_route_parameter_schema_and_responses()
    {
        await using BestStoriesApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/swagger/v1/swagger.json");
        JsonElement document = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement operation = document.GetProperty("paths")
            .GetProperty("/api/v1/best-stories")
            .GetProperty("get");
        JsonElement parameter = Assert.Single(operation.GetProperty("parameters").EnumerateArray());
        Assert.Equal("n", parameter.GetProperty("name").GetString());
        string[] statuses = operation.GetProperty("responses").EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        Assert.Contains("200", statuses);
        Assert.Contains("400", statuses);
        Assert.Contains("429", statuses);
        Assert.Contains("502", statuses);
        Assert.Contains("503", statuses);
        Assert.Contains("504", statuses);
    }
}
