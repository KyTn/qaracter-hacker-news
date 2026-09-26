using System.Text.Json.Serialization;

namespace Infrastructure.HackerNews;

[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(HackerNewsItemDto))]
internal sealed partial class HackerNewsJsonContext : JsonSerializerContext;
