# Requirements Evidence: Best Stories GET Endpoint

**Feature**: `002-best-stories-endpoint`  
**Reviewed**: 2026-09-26

## Product and Contract

- [x] SC-001 exact six-field JSON contract and nullable `uri`: `BestStoriesContractTests`
- [x] SC-002 `n` boundaries and zero client calls for invalid input: `BestStoriesValidationTests`
- [x] SC-003 first retrievable distinct feed candidates, score order, and stable ties:
  `BestStoriesSelectionTests` and `BestStoriesOrderingTests`
- [x] Empty and fewer-than-`n` results return successful arrays without fabrication.
- [x] OpenAPI publishes the route, query parameter, success response, and 400/429/502/503/504:
  `BestStoriesOpenApiTests`

## Upstream Protection

- [x] SC-004 per-request concurrency is bounded by configured batch size and the controlled
  endpoint run remains within the 001 global limit: `BestStoriesOrderingTests` and
  `BestStoriesCoalescingTests`
- [x] SC-005 100 concurrent cold HTTP requests produce exactly one feed population and one
  population per requested item; a fresh request with another `n` adds zero upstream calls:
  `BestStoriesCoalescingTests`
- [x] SC-006 no later batch begins once a completed batch supplies `n` stories:
  `BestStoriesOrderingTests`
- [x] Endpoint policy metadata and validated permit/queue bounds are verified by
  `BestStoriesAdmissionTests`; invalid bounds fail startup in `BestStoriesRegistrationTests`.
- [x] No final-response cache, orchestration retry, hedging, stale serving, Redis, background
  refresh, distributed locking, or live Hacker News test traffic was introduced.

## Failure and Cancellation

- [x] SC-008 invalid feed, timeout, unavailable, and throttled outcomes map to safe
  502/504/503 Problem Details without reason codes or upstream URLs:
  `BestStoriesFailureMappingTests`
- [x] Missing, invalid, deleted, dead, non-story, and unmappable candidates are skipped;
  transient dependency failures never return partial success.
- [x] Caller cancellation propagates into in-flight item work: `BestStoriesCancellationTests`

## Quality Gates

- [x] `dotnet restore src/Host.slnx`
- [x] `dotnet build src/Host.slnx --no-restore` (warning-clean final run required below)
- [x] `dotnet test tests/UnitTests/UnitTests.csproj --no-restore`
- [x] `dotnet test tests/IntegrationTests/IntegrationTests.csproj --no-restore`
- [x] Tests use controlled clients/handlers and do not contact the live Hacker News API.
- [x] Final format verification, warning-clean solution build, and complete solution test run.
