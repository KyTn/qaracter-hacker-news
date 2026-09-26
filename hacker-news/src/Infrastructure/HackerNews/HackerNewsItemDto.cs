using System.Text.Json.Serialization;

namespace Infrastructure.HackerNews;

internal sealed class HackerNewsItemDto
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("by")] public string? By { get; init; }
    [JsonPropertyName("time")] public long? Time { get; init; }
    [JsonPropertyName("score")] public long? Score { get; init; }
    [JsonPropertyName("descendants")] public long? Descendants { get; init; }
    [JsonPropertyName("deleted")] public bool Deleted { get; init; }
    [JsonPropertyName("dead")] public bool Dead { get; init; }
}
