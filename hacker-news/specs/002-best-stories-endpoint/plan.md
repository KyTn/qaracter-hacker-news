# Implementation Plan: Best Stories GET Endpoint

**Branch**: `002-best-stories-endpoint` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/002-best-stories-endpoint/spec.md`

## Summary

Add a thin ASP.NET Core GET boundary and an application use case that validate `n`, consume
the `IHackerNewsClient` created in specification 001, evaluate the ordered feed in bounded
batches, select the first `n` retrievable distinct candidates, and return them ordered by
score with deterministic feed-position ties. Protect high request volume with the built-in
ASP.NET Core concurrency rate limiter while retaining 001's separate HybridCache entries,
same-key coalescing, resilience pipeline, and process-wide outbound concurrency budget.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`)  
**Primary Dependencies**: ASP.NET Core 10 controllers, built-in rate-limiting middleware,
existing `IHackerNewsClient`, `Microsoft.Extensions.Caching.Hybrid`,
`Microsoft.Extensions.Http.Resilience`, options/validation/logging/metrics  
**Storage**: Existing in-process HybridCache L1 only; no database, response cache, or L2  
**Testing**: Existing xUnit `tests/UnitTests` and `tests/IntegrationTests`; application fakes
and controlled `HttpMessageHandler`; `WebApplicationFactory` for the HTTP boundary; no live calls  
**Target Platform**: Cross-platform ASP.NET Core service  
**Project Type**: Layered/hexagonal web service  
**Performance Goals**: 100 concurrent identical cold requests coalesce each feed/item key;
per-request item concurrency <= 8; process-wide upstream operations <= 16; fresh hits make
zero upstream calls; endpoint admits 100 executing plus 200 queued requests by default  
**Constraints**: `n` 1..100; exact public JSON names; progressive batch size 8; no stale
serving, hedging, orchestration retry, or final-response cache; safe bounded errors  
**Scale/Scope**: Initial single service instance; one versioned GET route and one application
use case reusing the two 001 client operations

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- [x] Public route, exact JSON contract, `n` range 1..100, partial-item rules, and stable
  score/feed-position ordering are explicit.
- [x] Batches of 8 scan the feed only until `n` retrievable distinct stories are collected;
  later batches are never started.
- [x] Feed, item, and negative keys/TTLs/capacity remain owned by 001; no final cache keyed
  by `n` fragments reuse.
- [x] HybridCache coalescing remains per service instance and concurrency tests cover that
  boundary.
- [x] The HTTP concurrency limiter defaults to 100 permits/200 FIFO queue entries; the use
  case defaults to 8 concurrent item operations; 001 retains the global 16/64 budget.
- [x] Timeout, retry, circuit breaker, cancellation, no-stale policy, partial failure, and
  HTTP mappings are explicit; orchestration adds no retries.
- [x] No distributed cache, background refresh, stale-if-error, or FusionCache is introduced.
- [x] Unit and controlled-upstream integration tests cover public contract, caching,
  coalescing, call bounds, admission, cancellation, observability, and failures.

## Project Structure

### Documentation (this feature)

```text
specs/002-best-stories-endpoint/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
`-- contracts/
    `-- best-stories-api.md
```

### Source Code (repository root)

```text
hacker-news/src/
|-- Application/
|   `-- BestStories/
|       |-- BestStoriesOptions.cs
|       |-- BestStoriesQuery.cs
|       |-- BestStoriesResult.cs
|       |-- BestStory.cs
|       |-- IBestStoriesService.cs
|       `-- BestStoriesService.cs
|-- Infrastructure/
|   `-- HackerNews/                    # existing 001 adapter; no new transport path
`-- Host/
    |-- Configuration/
    |   `-- BestStoriesOptionsValidator.cs
    |-- Controllers/
    |   `-- BestStoriesController.cs
    |-- DependencyInjection/
    |   `-- BestStoriesServiceCollectionExtensions.cs
    |-- Program.cs
    `-- appsettings.json

tests/
|-- UnitTests/
|   `-- BestStories/
`-- IntegrationTests/
    `-- BestStories/
```

**Structure Decision**: Preserve the existing three production projects. Application owns
validation, progressive orchestration, filtering, mapping, ordering, and closed outcomes.
Host owns options validation, HTTP binding, Problem Details, OpenAPI, inbound rate limiting,
configuration, and DI. Infrastructure remains unchanged except where integration tests reveal
a defect in the 001 contract. No new project or domain aggregate is justified.

## Design Decisions

### HTTP boundary and admission

- Expose `GET /api/v1/best-stories?n={n}` from a controller. Bind the raw query value so
  missing, repeated, malformed, and out-of-range input is rejected consistently before DI
  invokes the use case.
- Return `BestStory` DTOs using the exact camel-case property names from the coding test.
  Represent `uri` as a nullable URI string and `time` as `DateTimeOffset` normalized to UTC.
- Register one named concurrency-limiter policy with 100 permits, FIFO queue limit 200, and
  `429` rejection. Validate limits at startup and attach the policy only to this endpoint.
- Translate application outcomes centrally to RFC 9457 Problem Details: 400 invalid input,
  502 invalid feed, 503 unavailable/throttled, and 504 timeout. A disconnected caller does
  not receive a replacement response body.

### Progressive orchestration and ordering

- Fetch the feed once through `GetBestStoryIdsAsync`, preserving the returned order. Remove
  non-positive IDs defensively and duplicates after their first occurrence.
- Partition candidates into batches of `ItemConcurrency` (default 8). Start one client item
  operation per ID in the current batch, await the whole batch, preserve each candidate's
  feed index, and stop before starting another batch once `n` retrievable results exist.
- Skip definitive non-retrievable results and semantically unusable stories. Treat timeout,
  unavailable, throttled, and cancellation as terminal so the endpoint never substitutes a
  lower-priority story while presenting the response as complete.
- Select the first `n` retrievable stories by feed position, then order that selected set by
  score descending and feed position ascending. Use the item ID as a final defensive tie
  component even though feed positions are unique.
- Convert Unix seconds with `DateTimeOffset.FromUnixTimeSeconds`; invalid range makes that
  candidate non-retrievable. Default absent `descendants` to zero and keep absent URL null.

### Reuse of specification 001 protection

- Do not add cache or retry policy to the service. Every feed/item operation crosses the
  existing port, so cache keys, TTLs, negative caching, payload bounds, coalescing, attempt
  limits, timeouts, circuit breaker, and the global 16/64 outbound gate remain authoritative.
- Do not cache final arrays by `n`. Requests for different values reuse the same feed/item
  entries and cannot create a high-cardinality response cache.
- Request-owned task creation is bounded at 8. The client's singleton gate still bounds all
  physical upstream work across requests, including retry attempts.

### Configuration, diagnostics, and verification

- Add validated `BestStories` options for item concurrency (1..16), endpoint permit limit
  (positive), and queue limit (non-negative). Keep the public `n` maximum fixed at 100 for
  an unambiguous versioned contract.
- Emit one use-case duration/outcome metric and counters for examined, returned, and skipped
  candidates by low-cardinality reason. Let 001 retain upstream/cache metrics.
- Unit tests use an `IHackerNewsClient` fake and no Host dependencies. Integration tests use
  `WebApplicationFactory` plus the controlled upstream handler to verify the complete route,
  rate limiter, existing cache/coalescing, and global concurrency behavior.

## Post-Design Constitution Check

The design passes every pre-design gate. Selection follows the authoritative feed order,
presentation follows numeric score, and all work is bounded at input, endpoint admission,
per-request fan-out, process-wide outbound concurrency, attempts, payload, and time. The
existing reusable client and per-key cache are the only upstream path. No new infrastructure
or constitutional exception is required.

## Complexity Tracking

No constitutional violations or complexity exceptions are required.
