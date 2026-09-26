<!--
Sync Impact Report
- Version change: 1.0.0 -> 1.1.0
- Modified principles:
  - II. Upstream Protection and Efficient Concurrency - established default cache,
    coalescing, admission-control, and bounded-concurrency technologies
  - IV. Resilience and Explicit Failure Semantics - established the supported outbound
    resilience package and clarified stale-data behavior
- Added sections:
  - Reference Technology Decisions
- Removed sections: none
- Templates updated:
  - .specify/templates/plan-template.md - added explicit upstream-protection design gates
  - .specify/templates/spec-template.md - added mandatory upstream-protection requirements
  - .specify/templates/tasks-template.md - made tests mandatory and added protection tasks
- Templates reviewed without changes:
  - .specify/templates/checklist-template.md - compatible
- Runtime guidance reviewed:
  - AGENTS.md - compatible; points contributors to the active implementation plan
  - README.md - no governing references require changes
- Follow-up items: none
-->

# Hacker News Best Stories API Constitution

## Core Principles

### I. Source Requirements and API Contract

The supplied Developer Coding Test PDF is the product source of truth. The service MUST
provide a RESTful ASP.NET Core API that accepts a caller-supplied positive integer `n` and
returns exactly the best `n` retrievable Hacker News stories, or every retrievable story
when fewer than `n` are available. Stories MUST be selected from the Hacker News
`beststories` feed and returned in descending order of numeric score.

Each response item MUST expose the fields `title`, `uri`, `postedBy`, `time`, `score`, and
`commentCount`. Values MUST be mapped from the upstream story (`title`, `url`, `by`,
`time`, `score`, and `descendants` respectively); Unix time MUST be converted to an
ISO-8601 timestamp with an explicit offset. The public contract MUST NOT leak Hacker News
wire models or framework-specific implementation details. Input limits, missing or
deleted upstream items, absent optional fields, ties, and the behavior when fewer than
`n` valid stories exist MUST be specified before implementation. Ties MUST use a stable,
documented secondary ordering so repeated responses are deterministic.

### II. Upstream Protection and Efficient Concurrency

The API MUST efficiently serve large request volumes without risking overload of the
Hacker News API. Outbound calls MUST use managed, reusable HTTP clients; creating an HTTP
client per request is forbidden. The design MUST include caching with documented
freshness and staleness rules, request coalescing or equivalent stampede prevention, and
bounded concurrency for story-detail retrieval. Caller cancellation MUST propagate to
outbound work where safe.

The default cache implementation MUST be `Microsoft.Extensions.Caching.Hybrid` and cache
population MUST use its atomic `GetOrCreateAsync` flow so concurrent requests for the same
key share one in-flight factory execution within a service instance. The best-story ID
feed and individual story details MUST use separate cache entries so requests for
different values of `n` reuse upstream data. Implementations MUST NOT rely only on cached
final responses keyed by `n`. Cache durations MUST be configurable, use short freshness
windows appropriate to the data, and define separate negative-cache behavior for missing
or deleted items. Capacity or size limits MUST be configured for in-memory storage.

Implementations MUST NOT fetch more upstream data than is justified by the requested
result and Hacker News ordering semantics. Cache keys, lifetime, capacity behavior, and
concurrent population rules MUST be explicit and testable. Rate limiting, backpressure,
or another bounded admission strategy MUST protect both this service and its dependency.
Performance optimizations MUST preserve the response contract and score ordering.

ASP.NET Core's built-in rate-limiting middleware MUST provide bounded admission and a
bounded or zero-length queue. A process-wide outbound concurrency limit MUST cover all
Hacker News detail calls, including work from different inbound requests and retries;
per-request parallelism alone is insufficient. The limit MAY use
`System.Threading.RateLimiting` or a dedicated `SemaphoreSlim`, but its permits and queue
behavior MUST be configurable and verified under concurrent load.

### III. Clear Boundaries and Dependency Direction

Transport, application orchestration, Hacker News integration, and response mapping MUST
have explicit responsibilities. Controllers or endpoint handlers MUST validate and map
HTTP input, delegate the use case, and translate its result; they MUST NOT contain
caching policy, upstream-fetch algorithms, or business ordering logic. Hacker News wire
contracts MUST remain confined to the integration boundary and MUST be translated into
application-owned models before reaching the public API.

Core logic MUST depend on abstractions for external HTTP access, caching, and time where
those dependencies affect behavior. Concrete adapters and dependency registration MUST
remain outside the core. New projects, abstractions, packages, and patterns require a
demonstrated need; the smallest design that preserves testability, resilience, and these
boundaries MUST be preferred. Any exception MUST be documented in the implementation
plan's Complexity Tracking with scope, rationale, and the simpler alternative rejected.

### IV. Resilience and Explicit Failure Semantics

Every external call MUST have a finite timeout and a bounded retry policy appropriate to
transient failures. Retries MUST use backoff and MUST NOT amplify outages or bypass the
service's concurrency and rate limits. The service MUST define consistent HTTP outcomes
for invalid `n`, upstream timeout, upstream unavailability, malformed upstream data,
cancellation, and partial item failure. It MUST NOT silently return an unordered or
incorrectly ranked success response.

Serving stale cache data during an upstream failure is permitted only when the freshness
window and response semantics are documented and tested. Errors returned to callers MUST
be safe and actionable without exposing stack traces, secrets, internal URLs, or
implementation details. Configuration and secrets MUST come from supported configuration
providers and MUST NOT be committed to source control.

Outbound HTTP resilience MUST use `Microsoft.Extensions.Http.Resilience`; the deprecated
`Microsoft.Extensions.Http.Polly` package MUST NOT be introduced. Retry, timeout, and
circuit-breaker policies MUST be configured through the managed `HttpClient` pipeline.
Retries MUST remain inside the global outbound concurrency budget.

### V. Verification, Observability, and Documentation

Automated tests are a delivery gate. Unit tests MUST cover input boundaries, mapping,
time conversion, descending score order, deterministic ties, and missing upstream data.
Integration tests MUST exercise the HTTP contract against a controlled fake upstream and
verify caching, request coalescing, bounded fan-out, timeout, cancellation, and failure
mapping. Tests MUST be deterministic and MUST NOT depend on the live Hacker News API.

Structured logs and metrics MUST make request outcome, duration, cache result, upstream
call count, upstream latency, throttling, and failures diagnosable. Logs MUST NOT contain
secrets or unnecessary payload data. Health reporting MUST distinguish application
liveness from upstream dependency health when health endpoints are implemented.

The repository README MUST explain how to build, run, test, and call the API. It MUST
also record assumptions plus realistic enhancements or changes that would be made with
more time, as required by the coding test. OpenAPI documentation MUST accurately describe
the endpoint, validation constraints, success schema, and error responses.

## Technical and Product Constraints

- ASP.NET Core is mandatory; the exact supported .NET version MUST be declared in the
  implementation plan and README and pinned where practical.
- The official Hacker News API endpoints documented by the exercise are the external
  data source: `/v0/beststories.json` for candidate IDs and `/v0/item/{id}.json` for
  story details.
- The public response property names are `title`, `uri`, `postedBy`, `time`, `score`, and
  `commentCount`; contract changes require an explicit versioning decision.
- `n` MUST have a documented positive upper bound to prevent abusive fan-out, excessive
  latency, and memory pressure. Invalid values MUST fail before any upstream request.
- No database is required by the source exercise. Persistent storage, distributed cache,
  background refresh, or messaging MAY be introduced only when justified by measurable
  scale or deployment requirements.
- Functional requirements belong in feature specifications. This constitution governs
  their architecture, implementation quality, and verification.

## Reference Technology Decisions

- `Microsoft.Extensions.Caching.Hybrid` is the default cache abstraction for the .NET 10
  implementation. Its in-process stampede protection satisfies single-instance request
  coalescing; specifications MUST NOT claim that it coordinates cache population across
  service instances.
- A distributed cache such as Redis MAY be added behind `HybridCache` only when a
  multi-instance deployment or measured cache pressure justifies it. Shared storage alone
  MUST NOT be treated as distributed stampede protection.
- `ZiggyCreatures.FusionCache` MAY replace `HybridCache` only when the specification
  explicitly requires capabilities such as stale-if-error/fail-safe, eager refresh,
  per-entry jitter, soft factory timeouts, or distributed locking. The plan MUST record
  the requirement, operational trade-off, and rejected simpler `HybridCache` design in
  Complexity Tracking.
- ASP.NET Core's built-in rate limiter is the default inbound admission mechanism.
  Third-party rate-limiting packages require a documented capability gap.
- `Microsoft.Extensions.Http.Resilience` is the default outbound resilience integration.
  Policies MUST distinguish transient failures from permanent or malformed responses and
  MUST be tested against a controlled upstream.
- Fresh TTL, stale window when applicable, negative TTL, cache key format, capacity,
  concurrency permits, queue length, timeout, retry count, and circuit-breaker thresholds
  MUST come from validated configuration and have documented defaults.

## Development Workflow and Quality Gates

Every feature or material change MUST follow this sequence:

1. Specify independently testable acceptance criteria, edge cases, and error semantics.
2. Resolve material ambiguity around `n`, partial upstream data, ties, cache freshness,
   and upstream failure before implementation.
3. Pass the Constitution Check before research/design and repeat it after design.
4. Define the public HTTP contract and controlled-upstream test cases before adapters.
5. Design upstream call budgets, bounded concurrency, caching, coalescing, cancellation,
   timeouts, and retries before implementing the happy path.
6. Implement the smallest boundary-respecting solution and keep transport, orchestration,
   integration, and mapping concerns separate.
7. Run formatting, restore, build with warnings treated according to repository policy,
   static analysis, unit tests, and integration tests.
8. Verify deterministic descending ordering and the exact JSON contract through the HTTP
   boundary.
9. Verify with automated evidence that concurrent callers do not cause unbounded or
   duplicate upstream work.
10. Validate every command and assumption documented in the README.
11. Record justified deviations and remaining risks in the plan.

Reviews MUST reject a change when a mandatory gate lacks evidence, tests use the live
Hacker News service, upstream work is unbounded, or the API contract conflicts with the
coding test. A plan containing a constitutional exception MUST include a bounded
correction or removal strategy.

## Governance

This constitution supersedes incompatible sample text, implementation convenience, and
undocumented conventions. The Developer Coding Test PDF remains authoritative for product
behavior; if this constitution conflicts with it, the PDF wins and this document MUST be
amended. Every specification, plan, task list, review, and delivery decision MUST verify
compliance.

Amendments MUST update this file's Sync Impact Report and all affected templates or
guidance. Versioning follows semantic versioning: MAJOR for removal or incompatible
redefinition of a governing principle, MINOR for a new principle or materially expanded
obligation, and PATCH for a non-semantic clarification. Exceptions require written scope,
rationale, impact, approval, and a dated correction plan; an exception does not silently
amend the constitution. Compliance MUST be reviewed during planning, after design, in
code review, and before delivery.

**Version**: 1.1.0 | **Ratified**: 2026-09-26 | **Last Amended**: 2026-09-26
