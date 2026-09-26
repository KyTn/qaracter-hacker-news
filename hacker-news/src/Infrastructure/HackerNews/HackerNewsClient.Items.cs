using System.Diagnostics;
using System.Net;
using Application.HackerNews;
using Microsoft.Extensions.Caching.Hybrid;

namespace Infrastructure.HackerNews;

public sealed partial class HackerNewsClient
{
    public async ValueTask<HackerNewsResult<HackerNewsItem>> GetItemAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return HackerNewsResult<HackerNewsItem>.Failure(HackerNewsOutcome.InvalidInput, "item-id");

        long started = Stopwatch.GetTimestamp();
        string cacheKey = $"hn:item:{id}";
        try
        {
            CachedItemEnvelope envelope = await _cache.GetOrCreateAsync(
                cacheKey,
                async token => await FetchItemAsync(id, token).ConfigureAwait(false),
                new HybridCacheEntryOptions
                {
                    Expiration = _options.ItemCacheTtl,
                    LocalCacheExpiration = _options.ItemCacheTtl
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!envelope.Found)
            {
                await _cache.SetAsync(
                    cacheKey,
                    envelope,
                    new HybridCacheEntryOptions
                    {
                        Expiration = _options.MissingItemCacheTtl,
                        LocalCacheExpiration = _options.MissingItemCacheTtl
                    },
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
                RecordOutcome("item", HackerNewsOutcome.NotFound, started);
                return HackerNewsResult<HackerNewsItem>.Failure(HackerNewsOutcome.NotFound, "item-missing");
            }

            RecordSuccess("item", started);
            return HackerNewsResult<HackerNewsItem>.Success(envelope.Item!);
        }
        catch (Exception exception)
        {
            return MapFailure<HackerNewsItem>("item", exception, started, cancellationToken);
        }
    }

    private async ValueTask<CachedItemEnvelope> FetchItemAsync(long id, CancellationToken cancellationToken)
    {
        RecordCachePopulation("item");
        using HackerNewsConcurrencyGate.Lease lease = await _gate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired) throw new HackerNewsThrottledException();

        RecordUpstreamCall("item");
        using HttpRequestMessage request = new(HttpMethod.Get, $"v0/item/{id}.json");
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound) return CachedItemEnvelope.Missing();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Hacker News returned a non-success status.", null, response.StatusCode);

        HackerNewsItemDto? dto = await ReadJsonAsync(
            response,
            HackerNewsJsonContext.Default.HackerNewsItemDto,
            cancellationToken).ConfigureAwait(false);

        if (dto is null) return CachedItemEnvelope.Missing();
        if (dto.Id != id || dto.Id <= 0) throw new HackerNewsInvalidDataException();

        return CachedItemEnvelope.FromItem(new HackerNewsItem(
            dto.Id,
            dto.Type,
            dto.Title,
            dto.Url,
            dto.By,
            dto.Time,
            dto.Score,
            dto.Descendants,
            dto.Deleted,
            dto.Dead));
    }
}
