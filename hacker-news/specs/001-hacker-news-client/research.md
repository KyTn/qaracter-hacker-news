# Research: Hacker News Client

## Managed HTTP client

**Decision**: Register one transient typed client with `IHttpClientFactory` and inject the
configured `HttpClient` into the adapter.

**Rationale**: The factory centralizes logical-client configuration and manages pooled
handler lifetimes. Microsoft warns that typed clients must remain short-lived and must not
be captured by singleton services. This directly satisfies the constitution's reusable
managed-client requirement.

**Alternatives considered**:

- Constructing `HttpClient` per operation: rejected because it violates the constitution.
- A custom Hacker News client factory: rejected because it duplicates `IHttpClientFactory`.
- A singleton typed client: rejected because it can prevent handler/DNS lifetime renewal.

**Source**: [Microsoft: Use IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)

## Cache and request coalescing

**Decision**: Use `Microsoft.Extensions.Caching.Hybrid` with in-process storage and
`GetOrCreateAsync` for feed and item keys.

**Rationale**: `HybridCache` provides built-in stampede protection: concurrent callers for
one key on the same cache instance share one factory execution. It works without a
distributed cache and exposes maximum payload/key settings. Its coordination is limited
to one service instance, matching this feature's deployment scope.

**Alternatives considered**:

- `IMemoryCache` plus per-key semaphores: rejected because it recreates coalescing logic.
- FusionCache: rejected because stale fail-safe, eager refresh, and distributed locking are
  not required.
- Redis: rejected because no multi-instance deployment requirement exists.

**Source**: [Microsoft: Caching in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/caching)

## HTTP resilience

**Decision**: Use `Microsoft.Extensions.Http.Resilience` and its standard resilience
handler, customized for bounded retry, total/attempt timeouts, circuit breaker, and no
hedging.

**Rationale**: This is Microsoft's current HttpClient-specific resilience integration and
is built on Polly. The standard pipeline composes rate limiting, total timeout, retry,
circuit breaker, and attempt timeout. The older `Microsoft.Extensions.Http.Polly` package
is deprecated.

**Alternatives considered**:

- `Microsoft.Extensions.Http.Polly`: rejected because it is deprecated and forbidden.
- Hand-written retries and circuit state: rejected as unnecessary infrastructure.
- Hedging: rejected because parallel attempts amplify upstream load.

**Sources**:

- [Microsoft: Resilient app development](https://learn.microsoft.com/en-us/dotnet/core/resilience/)
- [Microsoft: HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)

## Upstream contract

**Decision**: Consume `/v0/beststories.json` as an ordered array of item IDs and
`/v0/item/{id}.json` as an item or null. Preserve type and deleted/dead fields so the
application can determine whether an item is retrievable.

**Rationale**: These are the endpoints and fields defined by the official Hacker News API
and the project constitution. Feed position is not treated as numeric-score evidence.

**Alternatives considered**: Top/new story feeds and HTML scraping were rejected because
they conflict with the product source of truth.

**Source**: [Official Hacker News API](https://github.com/HackerNews/API)

## Controlled testing

**Decision**: Use a deterministic scripted `HttpMessageHandler` for adapter tests and a
Host service-provider composition test for registration/options validation.

**Rationale**: A scripted handler precisely controls status, latency, cancellation,
payloads, attempt counts, and concurrent in-flight observations without network access.
The composition test catches lifetime and DI registration errors separately.

**Alternatives considered**:

- Live Hacker News tests: rejected as nondeterministic and constitutionally forbidden.
- A third-party HTTP mock library: deferred because a focused test double is sufficient.

