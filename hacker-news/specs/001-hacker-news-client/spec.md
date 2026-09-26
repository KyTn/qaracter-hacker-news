# Feature Specification: Hacker News Client

**Feature Branch**: `001-hacker-news-client`  
**Created**: 2026-09-26  
**Status**: Draft  
**Input**: User description: "Create the Hacker News client used to retrieve the best
story identifiers and individual story data according to the product document and project
constitution."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Retrieve Best Story Candidates (Priority: P1)

As the best-stories application, I can obtain the current ordered list of candidate story
identifiers from Hacker News so that later use cases can select stories from the required
source feed.

**Why this priority**: The candidate feed is the mandatory starting point for every valid
best-stories response.

**Independent Test**: Against a controlled upstream returning a known identifier list,
request the best-story candidates and verify that the same valid identifiers and order are
made available to the application without contacting the live Hacker News service.

**Acceptance Scenarios**:

1. **Given** the upstream returns a valid ordered identifier array, **When** the application
   requests best-story candidates, **Then** all identifiers are returned in upstream order.
2. **Given** the upstream returns an empty valid array, **When** candidates are requested,
   **Then** an empty result is returned without inventing stories.
3. **Given** the upstream response is malformed, **When** candidates are requested, **Then**
   the operation fails with a classified invalid-upstream-data outcome.

---

### User Story 2 - Retrieve a Story Detail (Priority: P1)

As the best-stories application, I can retrieve a story by its positive Hacker News item
identifier so that it can validate, rank, and map that story without depending on Hacker
News response models.

**Why this priority**: Candidate identifiers have no title, score, author, time, URL, or
comment count; story details are required to produce the public response.

**Independent Test**: Against a controlled upstream returning a known story, request one
identifier and verify the application receives all relevant values, including absent
optional values, in an application-owned representation.

**Acceptance Scenarios**:

1. **Given** a positive identifier for an existing story, **When** its detail is requested,
   **Then** the title, URL, author, Unix time, score, comment count, type, and deletion/dead
   indicators are made available to the application.
2. **Given** a positive identifier whose upstream value is null or missing, **When** its
   detail is requested, **Then** a not-found result is returned rather than a fabricated
   story or an unclassified failure.
3. **Given** an item with absent optional fields, **When** it is retrieved, **Then** absence
   is represented explicitly and no default value changes its meaning.
4. **Given** a non-positive identifier, **When** a detail is requested, **Then** validation
   fails before any upstream request.

---

### User Story 3 - Remain Predictable During Upstream Failure (Priority: P2)

As an API operator, I need upstream calls to finish within bounded time and return
classified failures so that the public API can remain responsive and apply a consistent
failure policy.

**Why this priority**: An unbounded or ambiguous dependency failure can exhaust service
resources and cannot be translated reliably at the HTTP boundary.

**Independent Test**: Configure a controlled upstream to delay, disconnect, return a
transient failure, and return malformed content; verify the resulting outcome and bounded
number of attempts for each case.

**Acceptance Scenarios**:

1. **Given** the upstream exceeds the configured response deadline, **When** either
   operation runs, **Then** it terminates with a timeout outcome within the configured bound.
2. **Given** a transient upstream failure, **When** a request is eligible for retry, **Then**
   attempts remain within the configured maximum and the final outcome is classified.
3. **Given** a permanent failure or malformed response, **When** an operation runs, **Then**
   it is not retried as though it were transient.
4. **Given** the caller cancels, **When** an upstream operation is still pending, **Then**
   cancellation propagates promptly and is distinguishable from timeout or unavailability.

### Edge Cases

- The best-story feed contains duplicate, zero, or negative identifiers.
- The feed or an item response exceeds configured payload limits.
- An item exists but is not a story, or is marked deleted or dead.
- Optional `url`, `descendants`, or other fields are absent or null.
- Numeric fields are outside the range accepted by the application.
- The upstream sends a success status with empty or malformed JSON.
- Many callers request the feed or the same item simultaneously.
- Many callers request different items and exhaust the outbound concurrency budget.
- The cache entry expires while concurrent callers are reading it.
- A caller disconnects while other callers share the same in-flight cache population.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST obtain candidate identifiers exclusively from the Hacker News
  `beststories` feed.
- **FR-002**: The system MUST retrieve item details by a caller-supplied positive item identifier.
- **FR-003**: The system MUST expose integration results through application-owned contracts;
  upstream wire representations MUST NOT cross the integration boundary.
- **FR-004**: The candidate operation MUST preserve upstream identifier order and MUST NOT
  rank identifiers without story-detail evidence.
- **FR-005**: The detail operation MUST preserve the distinction between absent optional
  values and actual zero or empty values.
- **FR-006**: The detail operation MUST make enough upstream information available to decide
  whether an item is a retrievable story, including type, deleted, and dead state.
- **FR-007**: Invalid identifiers MUST fail before initiating upstream work.
- **FR-008**: Missing or null items, invalid upstream data, timeout, unavailability, caller
  cancellation, and successful results MUST be distinguishable outcomes.
- **FR-009**: All dependency calls MUST have bounded duration and a bounded number of attempts.
- **FR-010**: Caller cancellation MUST propagate to dependency work when doing so does not
  cancel useful work shared by other active callers.
- **FR-011**: Configuration MUST define the upstream base address, request deadline, attempt
  limit, cache policy, admission limit, and outbound concurrency budget, and invalid
  configuration MUST prevent service startup.
- **FR-012**: Integration tests MUST use a controlled upstream and MUST NOT depend on the
  live Hacker News API.
- **FR-013**: The integration MUST emit diagnosable outcomes and timings without logging full
  payloads, secrets, or unnecessary internal addresses.
- **FR-014**: Unit tests MUST verify application-owned models, validation, mapping, and
  failure classification without network, cache, dependency-injection container, or clock
  dependencies.
- **FR-015**: Integration tests MUST verify dependency registration, configuration
  validation, HTTP translation, caching, coalescing, bounded concurrency, resilience, and
  cancellation across the real application and Infrastructure boundaries.

### Upstream Protection Requirements *(mandatory when calling Hacker News)*

- **UP-001**: This integration MUST NOT accept `n`; validation of the public positive upper
  bound belongs to the consuming best-stories use case before it invokes this integration.
- **UP-002**: Candidate feeds MUST use the key `hn:beststories`; item details MUST use
  `hn:item:{id}`; missing items MUST be negatively cached by the item key with an explicit
  missing result. Fresh TTL, negative TTL, capacity, and payload limits MUST be configurable.
- **UP-003**: Concurrent cache misses for the same key within one service instance MUST share
  one in-flight population. The specification does not require cross-instance coordination.
- **UP-004**: All callers in one service instance MUST share a configurable outbound
  concurrency budget. Admission MUST use a bounded queue, including the valid choice of no queue.
- **UP-005**: Timeout, retry eligibility, retry count, circuit opening, cancellation, invalid
  data, and missing-item behavior MUST be explicit and independently testable. This feature
  does not serve expired entries after their freshness period.
- **UP-006**: A fresh cache hit MUST cause zero upstream calls. Concurrent misses for one key
  MUST cause one logical cache-population call; any physical retries MUST remain within the
  configured attempt limit and concurrency budget.
- **UP-007**: Distributed cache, background refresh, stale-if-error, and distributed locking
  are outside this feature because no multi-instance or stale-serving requirement has been
  established.

### Key Entities

- **Best Story Candidate List**: Ordered collection of positive Hacker News item identifiers
  obtained from the required feed.
- **Hacker News Story Data**: Application-owned representation of upstream values needed to
  validate and later map a story: identifier, type, title, URL, author, Unix time, score,
  comment count, deleted state, and dead state.
- **Dependency Outcome**: Classified result distinguishing success, missing item, invalid
  input, invalid upstream data, timeout, unavailability, and cancellation.
- **Protection Policy**: Validated operational limits covering freshness, capacity,
  admission, concurrency, duration, attempts, and circuit behavior.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In controlled tests, 100% of valid candidate feeds and story responses retain
  their source values without leaking upstream-specific response objects to consumers.
- **SC-002**: In controlled tests with 100 simultaneous callers for the same uncached key,
  exactly one logical cache-population operation occurs within a service instance.
- **SC-003**: Fresh cache hits produce zero dependency requests in 100% of controlled tests.
- **SC-004**: Under 100 simultaneous requests for distinct uncached items, observed active
  dependency requests never exceed the configured global concurrency limit.
- **SC-005**: Timeout, cancellation, missing item, malformed response, transient failure, and
  permanent failure are each classified correctly in 100% of their controlled test cases.
- **SC-006**: Every dependency operation terminates within its configured total execution
  bound plus a documented scheduling tolerance.
- **SC-007**: Rejected input and rejected admission generate zero Hacker News calls.
- **SC-008**: Logs and diagnostic events expose outcome and duration while containing no
  complete upstream payloads or secrets in controlled verification.
- **SC-009**: Unit and integration test projects can be restored, built, and executed
  independently, and both are included in the solution-wide test run.

## Assumptions

- The official Hacker News endpoints are `/v0/beststories.json` and `/v0/item/{id}.json`.
- The consuming use case, not this integration, determines `n`, progressively fetches
  candidates, filters retrievable stories, sorts by score, and applies deterministic ties.
- A single service instance is the initial deployment target; distributed coordination is
  deliberately deferred until deployment evidence requires it.
- Expired data is not served in this feature. A later specification may introduce an
  explicitly bounded stale-data policy.
- Default numeric limits and durations are design-time decisions recorded by the plan and
  remain externally configurable and validated.
- Unit and integration tests are separate projects under `tests/`, outside production
  source folders; test projects are not referenced by production projects.
