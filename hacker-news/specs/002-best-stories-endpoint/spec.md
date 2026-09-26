# Feature Specification: Best Stories GET Endpoint

**Feature Branch**: `002-best-stories-endpoint`  
**Created**: 2026-09-26  
**Status**: Draft  
**Input**: User description: "Expose a GET endpoint that accepts `n` and returns the `n`
best retrievable Hacker News stories, reusing the client from specification 001 and
serving high request volume without overloading Hacker News."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the Best Stories (Priority: P1)

As an API consumer, I can request a positive number of best stories and receive up to that
many retrievable stories from the Hacker News `beststories` feed in descending score order.

**Why this priority**: This is the product capability required by the coding test.

**Independent Test**: Against the client port from specification 001 with a controlled feed
and controlled item results, call `GET /api/v1/best-stories?n=3` and verify the exact JSON
shape, story selection, ordering, and HTTP status without accessing live Hacker News.

**Acceptance Scenarios**:

1. **Given** at least `n` retrievable feed items, **When** a caller requests a valid `n`,
   **Then** the response is `200 OK` with exactly `n` stories selected in feed order and
   returned by descending numeric score.
2. **Given** fewer than `n` retrievable items in the feed, **When** a caller requests a
   valid `n`, **Then** the response is `200 OK` with every retrievable story and no
   fabricated entries.
3. **Given** stories with the same score, **When** the response is ordered, **Then** their
   relative order follows their original `beststories` feed position and is repeatable.
4. **Given** a valid story, **When** it is returned, **Then** its JSON contains exactly
   `title`, `uri`, `postedBy`, `time`, `score`, and `commentCount`; `time` is UTC ISO-8601.

---

### User Story 2 - Reject Unsafe Requests (Priority: P1)

As an API operator, I need invalid and excessive values of `n` rejected before dependency
work so a caller cannot cause uncontrolled fan-out or resource consumption.

**Why this priority**: Input bounds are part of both the public contract and upstream
protection.

**Independent Test**: Call the endpoint with missing, repeated, non-integer, zero, negative,
and greater-than-100 values and verify `400 Bad Request`, a safe Problem Details body, and
zero Hacker News client calls.

**Acceptance Scenarios**:

1. **Given** `n` is an integer from 1 through 100 inclusive, **When** the endpoint is called,
   **Then** the request is admitted to the use case.
2. **Given** `n` is absent, ambiguous, malformed, less than 1, or greater than 100, **When**
   the endpoint is called, **Then** it returns `400 Bad Request` before requesting the feed.
3. **Given** inbound capacity and its bounded queue are exhausted, **When** another request
   arrives, **Then** it returns `429 Too Many Requests` without starting use-case work.

---

### User Story 3 - Remain Correct Under Partial Data and Failure (Priority: P2)

As an API consumer, I receive either a correctly ranked result or a clear safe error when
Hacker News cannot provide enough trustworthy information.

**Why this priority**: Returning an apparently successful but incorrectly selected response
is worse than exposing a classified dependency failure.

**Independent Test**: Script missing, deleted, dead, non-story, malformed, timed-out,
unavailable, throttled, and canceled item outcomes and verify the documented continuation
or HTTP mapping for each one.

**Acceptance Scenarios**:

1. **Given** a candidate is missing, deleted, dead, not a story, or lacks the required public
   fields, **When** candidates are evaluated, **Then** it is skipped and later feed entries
   are considered until `n` retrievable stories are found or the feed is exhausted.
2. **Given** an item result is invalid upstream data, **When** candidates are evaluated,
   **Then** that candidate is skipped, diagnosed without logging its payload, and later
   candidates are considered.
3. **Given** a feed or item operation times out, **When** a correct result cannot be
   completed, **Then** the endpoint returns `504 Gateway Timeout` with safe Problem Details.
4. **Given** the dependency is unavailable or the client rejects local admission, **When**
   a correct result cannot be completed, **Then** the endpoint returns `503 Service
   Unavailable` and does not return a partial ranked success.
5. **Given** the caller disconnects, **When** work is pending, **Then** cancellation stops
   request-owned orchestration promptly and propagates to client calls where safe.

---

### User Story 4 - Reuse Upstream Work at High Load (Priority: P2)

As an API operator, I can serve many concurrent callers while bounding local admission and
Hacker News traffic.

**Why this priority**: The coding test explicitly requires efficient service without
putting the upstream API at risk.

**Independent Test**: Start 100 concurrent identical requests against a controlled client
and verify bounded endpoint admission, per-request fan-out, reuse of the 001 feed/item
cache entries, and no duplicate population for the same key within one service instance.

**Acceptance Scenarios**:

1. **Given** concurrent requests for different values of `n`, **When** they overlap, **Then**
   they reuse the separate feed and item cache entries from specification 001 rather than
   depending on final-response cache entries keyed by `n`.
2. **Given** one admitted request, **When** it loads uncached item details, **Then** it has at
   most 8 item operations in flight and the client from 001 still enforces its process-wide
   limit of 16 across all requests and retries.
3. **Given** enough retrievable stories have been found, **When** more feed candidates remain,
   **Then** the use case does not start another batch of item operations.

### Edge Cases

- `n` is missing, repeated, whitespace, non-numeric, outside the `Int32` range, 0, -1, 100,
  or 101.
- The feed is empty, contains duplicates, or contains fewer than `n` identifiers.
- The same identifier appears more than once; it is evaluated only at its first feed position.
- An item is null, deleted, dead, not type `story`, has no title/author/time/score, or has a
  missing URL or comment count.
- Missing URL is represented as `null`; missing comment count is represented as `0`. Missing
  title, author, time, or score makes an item non-retrievable.
- Unix time cannot be represented as a valid UTC timestamp.
- Scores are equal, negative, or change between cache refreshes.
- A batch contains both retrievable and malformed/missing candidates.
- Cancellation occurs between batches or while a batch is in flight.
- The feed or item cache expires during concurrent requests; semantics remain those of 001.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose `GET /api/v1/best-stories` with exactly one required
  query parameter named `n`.
- **FR-002**: `n` MUST be a single base-10 integer in the inclusive range 1..100; every
  other representation MUST return `400 Bad Request` before any `IHackerNewsClient` call.
- **FR-003**: The use case MUST obtain the ordered candidate identifiers and item details
  exclusively through the `IHackerNewsClient` contract defined by specification 001.
- **FR-004**: Candidate priority MUST follow first occurrence in the `beststories` feed.
  Details MUST be fetched in bounded batches until `n` retrievable stories are found or the
  deduplicated feed is exhausted.
- **FR-005**: A retrievable story MUST be an existing, non-deleted, non-dead item of type
  `story` with a non-empty title and author plus representable time and score. URL MAY be
  null; absent comment count MUST map to zero.
- **FR-006**: Missing, deleted, dead, non-story, unmappable, and invalid-data candidates MUST
  be skipped. Timeout, unavailable, throttled, or canceled outcomes MUST NOT be hidden as a
  partial successful response.
- **FR-007**: The response MUST contain at most `n` distinct stories, selected by feed
  position, then ordered by score descending with original feed position ascending as the
  stable tie-breaker.
- **FR-008**: Each response item MUST expose exactly `title`, `uri`, `postedBy`, `time`,
  `score`, and `commentCount`, mapped from the application-owned item model. `time` MUST be
  emitted as a UTC ISO-8601 timestamp with an explicit offset.
- **FR-009**: Successful responses MUST be `200 OK` JSON arrays, including `[]` when no
  story is retrievable.
- **FR-010**: Validation failures MUST return `400`; endpoint admission rejection MUST
  return `429`; invalid feed data MUST return `502`; timeout MUST return `504`; and
  unavailable or locally throttled dependency outcomes MUST return `503`, all using safe
  RFC 9457 Problem Details without internal URLs, payloads, or stack traces.
- **FR-011**: Request cancellation MUST be honored between batches and passed to every
  client operation. The application MUST NOT translate a disconnected caller into a new
  response body.
- **FR-012**: OpenAPI MUST document the route, `n` range, success schema, nullable `uri`, and
  all declared error responses.
- **FR-013**: Structured diagnostics MUST record duration, requested `n`, examined candidate
  count, returned count, skipped count by safe reason, outcome, and admission rejection;
  they MUST NOT record payloads or unnecessary upstream addresses.
- **FR-014**: Unit tests MUST cover validation, deduplication, batching, filtering, mapping,
  time conversion, ordering, ties, stop conditions, and result classification.
- **FR-015**: Integration tests MUST cover the real HTTP contract, Problem Details, OpenAPI,
  inbound admission, concurrent callers, cancellation, and the cache/coalescing and global
  outbound bounds supplied by specification 001, using no live Hacker News calls.

### Upstream Protection Requirements *(mandatory when calling Hacker News)*

- **UP-001**: The positive upper bound for `n` is 100 and invalid input MUST initiate zero
  feed or item operations.
- **UP-002**: This feature MUST reuse 001 keys and defaults: `hn:beststories` for 30 seconds,
  `hn:item:{id}` for 5 minutes, missing items for 30 seconds, 1 MiB payload limit, and
  128-character key limit. It MUST NOT rely on a final-response cache keyed by `n`.
- **UP-003**: Cache population MUST continue to use `HybridCache.GetOrCreateAsync`; same-key
  misses share one population only within a single service instance.
- **UP-004**: Per-request item concurrency MUST default to 8 and be configurable from 1..16.
  The 001 process-wide outbound limit (default 16, FIFO queue 64) remains authoritative.
  ASP.NET Core's concurrency rate limiter MUST default to 100 executing requests and a
  FIFO queue of 200, with startup validation for all limits.
- **UP-005**: The endpoint MUST inherit the bounded timeout, retry, circuit-breaker,
  cancellation, and fresh-only cache semantics from 001. It MUST add no retry, stale
  serving, hedging, or background refresh at the orchestration layer.
- **UP-006**: A fresh feed/item cache hit MUST cause zero physical Hacker News calls; same-key
  concurrent misses MUST coalesce as in 001. One request MUST start at most one item lookup
  per distinct examined identifier and MUST stop scheduling new batches after collecting
  `n` retrievable stories.
- **UP-007**: Distributed cache, distributed locking, background refresh, stale-if-error,
  and final-response caching are outside this single-instance feature because no measured
  deployment need justifies them.

### Key Entities

- **Best Stories Query**: Validated request containing `n` in the range 1..100.
- **Ranked Story**: Public story representation plus its internal original feed position
  used only for deterministic ordering.
- **Candidate Evaluation**: Result of evaluating a distinct feed identifier as retrievable,
  definitively skipped, or terminally failed.
- **Best Stories Outcome**: Closed application result translated by the HTTP boundary into
  success, invalid input, bad gateway, timeout, unavailable, throttled, or cancellation.
- **Admission Policy**: Validated endpoint concurrency permits, FIFO queue length, and
  per-request item concurrency.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every controlled valid case, the endpoint returns exactly the documented
  property names and no upstream or framework-specific fields.
- **SC-002**: Across boundary tests for `n`, 100% of invalid requests produce `400` and zero
  Hacker News client calls.
- **SC-003**: Across generated feeds, the result contains the first `n` retrievable distinct
  feed items (or all available), ordered by descending score with stable feed-order ties.
- **SC-004**: One request never observes more than 8 concurrent item operations by default,
  and 100 concurrent HTTP requests never cause the 001 process-wide outbound limit of 16
  to be exceeded.
- **SC-005**: With 100 concurrent identical cold requests in one instance, each distinct
  feed/item cache key has one logical cache-population operation; fresh repetitions cause
  zero additional physical Hacker News requests.
- **SC-006**: Once the `n`th retrievable story completes a batch, no identifier in a later
  batch is requested; each examined distinct identifier is requested at most once per query.
- **SC-007**: Exhausting 100 endpoint permits plus the 200-request queue produces `429` for
  further arrivals without starting their use cases.
- **SC-008**: Timeout, dependency unavailability, malformed feed, local dependency
  throttling, and caller cancellation match their documented outcomes in every controlled
  integration test, without returning a misleading partial success.

## Assumptions

- The `beststories` array order defines candidate priority. Scores determine presentation
  order among the selected retrievable candidates; the API does not claim that score alone
  defines the feed's concept of "best."
- The implementation from specification 001 is present and remains the sole Hacker News
  integration boundary.
- Initial deployment is one service instance. Multi-instance cache coordination requires a
  later specification backed by deployment evidence.
- The maximum `n` of 100 balances the coding-test use case with bounded latency and fan-out
  and is externally configurable only through a future contract-versioning decision.
- Authentication, persistence, Redis, background refresh, and stale serving are out of scope.

