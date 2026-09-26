# Hacker News Best Stories API

ASP.NET Core service targeting .NET 10. It exposes a bounded best-stories endpoint backed by
the official Hacker News API. The service uses per-key caching, request coalescing, bounded
endpoint and outbound concurrency, retries, timeouts, circuit breaking, cancellation, and
classified dependency outcomes.

## Intellectual Property Notice

> This repository has been created exclusively as a technical assessment for Qaracter. Except
> for third-party components and any rights that may have been expressly assigned by contract,
> the original code and documentation contained herein are the property of their author. No
> copying, reproduction, modification, distribution, publication, or exploitation, in whole or
> in part, is authorized without the prior written consent of the rights holder. The rights
> holder reserves the right to pursue any legal remedies available in response to unauthorized
> use.

## API

```http
GET /api/v1/best-stories?n=10
Accept: application/json
```

`n` is required exactly once and must be an integer from 1 through 100. A successful response
is a JSON array containing up to `n` retrievable stories with exactly these fields:
`title`, nullable `uri`, `postedBy`, UTC `time`, `score`, and `commentCount`. Candidate priority
comes from the `beststories` feed; the selected stories are returned by descending score with
feed position as the stable tie-breaker.

Invalid input returns `400`, exhausted endpoint admission returns `429`, invalid upstream feed
data returns `502`, dependency unavailability returns `503`, and dependency timeout returns
`504`, all as safe Problem Details.

## Build and test

```powershell
dotnet restore src/Host.slnx
dotnet build src/Host.slnx --no-restore
dotnet test tests/UnitTests/UnitTests.csproj --no-build
dotnet test tests/IntegrationTests/IntegrationTests.csproj --no-build
```

Tests use controlled HTTP handlers and never call the live Hacker News API.

## Run locally

```powershell
dotnet run --project src/Host/Api.csproj
```

Swagger UI is available at `/swagger` when the application runs in the Development
environment.

Runtime values for cache TTLs, payload size, outbound concurrency, retries, timeouts, and
circuit breaking live under `HackerNews`; per-request concurrency and endpoint admission live
under `BestStories`. Both sections in `src/Host/appsettings.json` are validated during startup.

## Assumptions

1. We assume that the order returned by `beststories` represents the ranking that should be
   used. Therefore, the service selects the first `n` retrievable stories without downloading
   the details of the entire feed.
2. The identifier feed is cached. Changes to its order become visible on the first request
   after the feed cache entry reaches its TTL.
3. Each story is cached independently by ID. Changes to a story become visible on the first
   request after that story's cache entry reaches its own TTL.
4. The response is ordered using the cached scores of the selected stories.

## Protecting the Hacker News API

The service uses three complementary mechanisms:

1. `HybridCache` caches the feed and individual items with configurable TTLs.
2. `HybridCache.GetOrCreateAsync` coalesces concurrent cache population. Multiple requests for
   the same missing or expired key share one population operation within a service instance,
   preventing a cache stampede.
3. Concurrency limits protect both the application and its dependency:
   - `HackerNewsConcurrencyGate` globally limits outbound operations to Hacker News.
   - The HTTP rate limiter limits inbound requests and uses a bounded queue.

`HackerNewsConcurrencyGate` does not prevent cache stampedes; it prevents saturation of the
external service. `HybridCache` coalescing prevents duplicate loads for the same cache key.

Timeouts, bounded retries, and a circuit breaker additionally prevent a degraded Hacker News
API from consuming resources indefinitely. The Hacker News `HttpClient` uses
`AddStandardResilienceHandler` with the following pipeline:

```text
Rate limiter -> Total timeout -> Retry -> Circuit breaker -> Attempt timeout
```

## Eventual consistency

The feed and individual items have independent TTLs, so the system intentionally accepts a
window of eventual consistency.

For example, assume the initial feed is `A, B, C`. A request with `n=3` caches the following
values and returns them in this order:

```text
A=30, B=20, C=10
```

The feed entry then expires and is refreshed from Hacker News. Its new order is `C, A, B`, but
the item entries remain cached with `C=10`, `A=30`, and `B=20`. A request with `n=2` selects
`C` and `A`, then orders them using their cached scores, producing:

```text
A=30, C=10
```

When the cache entry for `C` expires, its current score is fetched. If the new score is 100,
the service can then return the up-to-date order:

```text
C=100, A=30
```

## Possible improvements

- Accept eventual consistency and use a shorter item TTL. This is the simplest and most
  balanced option.
- Configure the item TTL to be equal to or shorter than the feed TTL. This reduces the
  inconsistency window, although it cannot guarantee exact synchronization because entries
  are created at different times.
- Force a refresh only for the leading candidates when the feed changes.
- Introduce a feed generation in item cache keys, for example
  `hn:item:{feedGeneration}:{id}`. This guarantees renewed item data after a feed change but
  significantly increases traffic.
- Invalidate every item when the feed is refreshed. This is possible but overly aggressive:
  a change in feed order does not mean that every item has changed.

For this exercise, accepting eventual consistency and reducing the item TTL if necessary is
a reasonable trade-off between freshness, performance, and protection of the Hacker News API.

## Scope

The implementation assumes a single service instance and deliberately excludes Redis,
distributed stampede coordination, final-response caching by `n`, background refresh, and
stale-if-error behavior. Those capabilities should be introduced only when supported by
measured deployment or availability requirements.
