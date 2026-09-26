using Application.HackerNews;

namespace Infrastructure.HackerNews;

internal sealed record CachedItemEnvelope(bool Found, HackerNewsItem? Item)
{
    public static CachedItemEnvelope Missing() => new(false, null);
    public static CachedItemEnvelope FromItem(HackerNewsItem item) => new(true, item);
}
