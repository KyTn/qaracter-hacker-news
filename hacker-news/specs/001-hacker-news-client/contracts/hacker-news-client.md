# Contract: Hacker News Client Port

This is an internal application port. It does not add a public REST endpoint.

## Operations

```csharp
public interface IHackerNewsClient
{
    ValueTask<HackerNewsResult<IReadOnlyList<long>>> GetBestStoryIdsAsync(
        CancellationToken cancellationToken);

    ValueTask<HackerNewsResult<HackerNewsItem>> GetItemAsync(
        long id,
        CancellationToken cancellationToken);
}
```

## Behavioral contract

### `GetBestStoryIdsAsync`

- Reads only the official best-stories feed.
- Returns an immutable ordered list on success, including a valid empty list.
- Treats a non-array root, null root, non-integer value, non-positive identifier, oversized
  response, or invalid JSON as `InvalidUpstreamData`.
- Uses cache key `hn:beststories` with the configured feed TTL.

### `GetItemAsync`

- Rejects `id <= 0` as `InvalidInput` before cache or HTTP access.
- Reads only the official item path for the supplied identifier.
- Returns `NotFound` for an HTTP 404 or a successful JSON `null` response and negatively
  caches that outcome for the configured missing TTL.
- Verifies that a non-null item's `id` equals the requested identifier.
- Preserves absent optional fields and returns `InvalidUpstreamData` for schema violations.
- Uses cache key `hn:item:{id}`.

## Shared semantics

- Fresh hits make no upstream request.
- Concurrent misses for the same key share one logical population per service instance.
- Caller cancellation returns `Canceled`; a canceled waiter does not cancel shared work
  still needed by another waiter.
- Local admission rejection returns `Throttled` without contacting Hacker News.
- Attempt exhaustion, circuit rejection, and terminal transient HTTP responses return
  `Unavailable`; deadline expiry returns `Timeout`.
- Results never expose wire DTOs, response bodies, dependency URLs, or exceptions.

## Upstream paths

```text
GET /v0/beststories.json
GET /v0/item/{id}.json
```

Both requests accept JSON only. Redirects are not followed to a different origin.

