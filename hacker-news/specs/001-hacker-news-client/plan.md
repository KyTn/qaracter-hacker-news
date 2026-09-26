# Implementation Plan: Hacker News Client

**Branch**: `001-hacker-news-client` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-hacker-news-client/spec.md`

## Summary

Create the application port and Infrastructure adapter that retrieve the Hacker News best
story feed and individual items. Register the adapter as a typed, transient HTTP client
managed by `IHttpClientFactory`, decorate upstream reads with `HybridCache`, and enforce
validated timeout, retry, circuit-breaker, payload, and process-wide concurrency limits.
The adapter translates private wire DTOs into application-owned result models and exposes
classified failures without adding a public HTTP endpoint or implementing ranking by `n`.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`)  
**Primary Dependencies**: ASP.NET Core 10, `Microsoft.Extensions.Http`,
`Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Caching.Hybrid`, built-in
options/validation/logging/rate-limiting abstractions  
**Storage**: In-process `HybridCache` L1 only; no database or distributed cache  
**Testing**: Separate xUnit `tests/UnitTests` and `tests/IntegrationTests` projects using
`Microsoft.NET.Test.Sdk`; controlled `HttpMessageHandler` and Host DI composition tests;
no live Hacker News calls  
**Target Platform**: Cross-platform ASP.NET Core service  
**Project Type**: Layered/hexagonal web service  
**Performance Goals**: 100 callers for one cold key coalesce to one logical population;
active Hacker News operations never exceed the configured default of 16; fresh hits make
zero upstream calls  
**Constraints**: Feed TTL 30 s, item TTL 5 min, missing-item TTL 30 s; 1 MiB maximum cached
payload and HTTP body; 128-character maximum cache key; 2 s attempt timeout, 5 s total
timeout, at most 2 retries; all values validated and configurable  
**Scale/Scope**: Single service instance; two upstream GET operations; no public endpoint,
ranking orchestration, Redis, background refresh, stale serving, or distributed locking

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- [x] Public contract, `n` bounds, partial-item behavior, and deterministic ordering are
  outside this adapter and explicitly assigned to the consuming use case in the spec.
- [x] The adapter fetches only the requested feed or item; progressive fetching and score
  ordering remain with the consuming use case.
- [x] Feed key `hn:beststories`, item key `hn:item:{id}`, fresh/negative TTLs, key length,
  and payload limits are explicit.
- [x] `HybridCache.GetOrCreateAsync` provides per-instance coalescing and concurrent tests
  verify exactly one logical population for the same key.
- [x] A singleton outbound concurrency gate limits all upstream operations to 16 with a
  bounded queue of 64; inbound admission is deferred because this feature exposes no route.
- [x] Attempt/total timeout, bounded retry, circuit breaker, cancellation, no-stale policy,
  and classified failures are explicit.
- [x] Distributed cache, background refresh, and FusionCache are excluded, so no complexity
  exception is required.
- [x] Controlled-handler tests cover cache behavior, coalescing, global concurrency,
  resilience, cancellation, translation, logging, and metrics without live calls.

## Project Structure

### Documentation (this feature)

```text
specs/001-hacker-news-client/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── hacker-news-client.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
hacker-news/src/
├── Application/
│   └── HackerNews/
│       ├── IHackerNewsClient.cs
│       ├── HackerNewsItem.cs
│       └── HackerNewsResult.cs
├── Infrastructure/
│   └── HackerNews/
│       ├── HackerNewsClient.cs
│       ├── HackerNewsItemDto.cs
│       ├── HackerNewsJsonContext.cs
│       └── HackerNewsConcurrencyGate.cs
└── Host/
    ├── Configuration/
    │   └── HackerNewsOptions.cs
    ├── DependencyInjection/
    │   └── HackerNewsServiceCollectionExtensions.cs
    └── appsettings.json

tests/
├── UnitTests/
│   └── UnitTests.csproj
└── IntegrationTests/
    └── IntegrationTests.csproj
    └── HackerNews/
```

**Structure Decision**: Preserve the existing solution layout under `hacker-news/src`.
Application owns the port and dependency-neutral models; Infrastructure implements the
adapter; Host owns configuration validation and dependency registration. Add test projects
at repository level and reference production projects according to dependency direction.
No Domain changes are required because upstream transport data is not a domain aggregate.
`UnitTests` references Application only and exercises isolated contracts and mapping logic.
`IntegrationTests` references Application, Infrastructure, and Host to exercise real DI,
HTTP, caching, concurrency, and resilience boundaries against a controlled handler.

## Design Decisions

### Dependency registration and lifetime

- Register `HackerNewsClient` through `AddHttpClient<IHackerNewsClient, HackerNewsClient>`;
  do not register it a second time and do not create a custom client factory.
- Keep the typed client transient so it is never captured by a singleton. `HybridCache` and
  `HackerNewsConcurrencyGate` hold the shared per-process state.
- Configure the base address and headers centrally from validated `HackerNewsOptions`.
- Add one standard resilience handler with hedging disabled. Retrying parallel hedges would
  spend unnecessary Hacker News calls.

### Caching

- Use `HybridCache.GetOrCreateAsync` for both operations with immutable cached value types.
- Use `hn:beststories` for the feed and `hn:item:{id}` for an item. A cacheable envelope
  represents both found and missing items so `null` is never ambiguous.
- Feed entries expire after 30 seconds, found items after 5 minutes, and missing items after
  30 seconds. No expired entry is served.
- Configure `MaximumPayloadBytes = 1_048_576` and `MaximumKeyLength = 128`; reject HTTP
  bodies over 1 MiB before deserialization. No L2 cache is configured.
- Pass each caller token to `GetOrCreateAsync`; the shared factory token protects useful
  work for remaining waiters while allowing an individual waiter to cancel.

### Resilience and concurrency

- A singleton gate is acquired inside the cache factory and outside `HttpClient.SendAsync`,
  so its permit covers all physical retry attempts produced by that logical operation.
- Default gate: 16 permits and FIFO queue of 64. Queue rejection produces a classified
  throttled outcome without contacting Hacker News.
- Default resilience: 2-second attempt timeout, 5-second total timeout, maximum 2 retries
  with exponential backoff starting at 200 ms and jitter, and circuit breaking after a
  configurable failure threshold.
- Retry only transport failures, attempt timeouts, HTTP 408, 429, and 5xx. Do not retry
  caller cancellation, invalid identifiers, 4xx other than 408/429, successful malformed
  bodies, missing items, or semantic validation failures.
- Dispose every unsuccessful `HttpResponseMessage`; stream successful JSON with bounded
  content instead of buffering unbounded payloads.

### Boundary and failure mapping

- `HackerNewsItemDto` and JSON metadata remain internal to Infrastructure.
- `HackerNewsItem` retains nullable optional fields and raw Unix time; public response
  mapping and time conversion belong to the later best-stories use case.
- `HackerNewsResult<T>` carries a closed outcome kind: success, not found, invalid input,
  invalid upstream data, timeout, unavailable, throttled, or canceled. It contains no
  exception, internal URL, or response body.
- Logs and meters record operation name, classified outcome, duration, cache status where
  available, upstream attempt count, and throttling; they do not record payloads.

## Post-Design Constitution Check

The design continues to pass every pre-design gate. It adds no database, distributed
coordination, background service, custom factory, or third-party resilience/cache library.
Retry work remains inside the process-wide permit, cache population is coalesced per key,
and every mandatory behavior has a deterministic controlled-upstream test path.

## Complexity Tracking

No constitutional violations or complexity exceptions are required.
