using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Application.HackerNews;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.HackerNews;

public sealed partial class HackerNewsClient(
    HttpClient httpClient,
    HybridCache cache,
    HackerNewsConcurrencyGate gate,
    IOptions<HackerNewsOptions> options,
    ILogger<HackerNewsClient> logger) : IHackerNewsClient
{
    private const string BestStoriesKey = "hn:beststories";
    private readonly HackerNewsOptions _options = options.Value;

    public async ValueTask<HackerNewsResult<IReadOnlyList<long>>> GetBestStoryIdsAsync(
        CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            BestStoryIdsCacheEntry entry = await cache.GetOrCreateAsync(
                BestStoriesKey,
                FetchBestStoryIdsAsync,
                new HybridCacheEntryOptions
                {
                    Expiration = _options.FeedCacheTtl,
                    LocalCacheExpiration = _options.FeedCacheTtl
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            RecordSuccess("beststories", started);
            return HackerNewsResult<IReadOnlyList<long>>.Success(Array.AsReadOnly(entry.Ids));
        }
        catch (Exception exception)
        {
            return MapFailure<IReadOnlyList<long>>("beststories", exception, started, cancellationToken);
        }
    }

    private async ValueTask<BestStoryIdsCacheEntry> FetchBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        RecordCachePopulation("beststories");
        using HackerNewsConcurrencyGate.Lease lease = await gate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired) throw new HackerNewsThrottledException();

        RecordUpstreamCall("beststories");
        using HttpRequestMessage request = new(HttpMethod.Get, "v0/beststories.json");
        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Hacker News returned a non-success status.", null, response.StatusCode);

        long[]? ids = await ReadJsonAsync(response, HackerNewsJsonContext.Default.Int64Array, cancellationToken)
            .ConfigureAwait(false);

        if (ids is null || ids.Any(static id => id <= 0))
            throw new HackerNewsInvalidDataException();

        return new BestStoryIdsCacheEntry(ids);
    }

    private async ValueTask<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > _options.MaximumPayloadBytes)
            throw new HackerNewsInvalidDataException();

        try
        {
            await response.Content.LoadIntoBufferAsync(_options.MaximumPayloadBytes, cancellationToken)
                .ConfigureAwait(false);
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or HttpRequestException)
        {
            throw new HackerNewsInvalidDataException(exception);
        }
    }
}

internal sealed class HackerNewsInvalidDataException : Exception
{
    public HackerNewsInvalidDataException() { }
    public HackerNewsInvalidDataException(Exception innerException) : base("Invalid Hacker News response.", innerException) { }
}

internal sealed class HackerNewsThrottledException : Exception;
