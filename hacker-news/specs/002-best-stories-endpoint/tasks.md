# Tasks: Best Stories GET Endpoint

**Input**: Design documents from `specs/002-best-stories-endpoint/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Automated tests are mandatory. Application tests use a fake
`IHackerNewsClient`; HTTP/integration tests use the controlled upstream from specification
001 and never contact the live Hacker News API.

**Organization**: Tasks are grouped by user story so validation, failure behavior, and load
protection remain independently demonstrable around the P1 endpoint capability.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no incomplete dependency.
- **[Story]**: Maps the task to US1, US2, US3, or US4 in `spec.md`.
- Every task names the exact file or files it creates or modifies.

## Phase 1: Setup

**Purpose**: Add only the test and Host wiring prerequisites not already supplied by 001.

- [x] T001 Add `Microsoft.AspNetCore.Mvc.Testing` with a .NET 10-compatible version to `tests/IntegrationTests/IntegrationTests.csproj`
- [x] T002 [P] Create the `tests/UnitTests/BestStories/` and `tests/IntegrationTests/BestStories/Support/` test namespaces with shared global usings only where required
- [x] T003 [P] Create the feature evidence checklist in `specs/002-best-stories-endpoint/checklists/requirements.md` with entries for SC-001 through SC-008 and the constitutional protection gates
- [x] T004 Verify `src/Host.slnx` still discovers Application, Infrastructure, Host, UnitTests, and IntegrationTests before feature work begins

---

## Phase 2: Foundational

**Purpose**: Establish application contracts, configuration, deterministic fakes, and Host
composition required by every user story.

**CRITICAL**: No user-story implementation begins until this phase is complete.

- [x] T005 [P] Implement immutable `BestStoriesQuery`, `BestStory`, and the closed `BestStoriesResult` outcome model in `src/Application/BestStories/BestStoriesQuery.cs`, `src/Application/BestStories/BestStory.cs`, and `src/Application/BestStories/BestStoriesResult.cs`
- [x] T006 [P] Define `IBestStoriesService.GetAsync(BestStoriesQuery, CancellationToken)` in `src/Application/BestStories/IBestStoriesService.cs`
- [x] T007 [P] Define `BestStoriesOptions` defaults for item concurrency 8, endpoint permits 100, and FIFO queue length 200 in `src/Application/BestStories/BestStoriesOptions.cs`
- [x] T008 [P] Add unit tests for query/result invariants and fixed `n` boundaries in `tests/UnitTests/BestStories/BestStoriesContractTests.cs`
- [x] T009 [P] Implement a deterministic fake `IHackerNewsClient` that scripts feed/item outcomes and records IDs, call counts, active count, and cancellation in `tests/UnitTests/BestStories/Support/FakeHackerNewsClient.cs`
- [x] T010 [P] Implement a `WebApplicationFactory<Program>` fixture that replaces the live Hacker News handler/configuration and exposes controlled request counters in `tests/IntegrationTests/BestStories/Support/BestStoriesApiFactory.cs`
- [x] T011 Add `BestStories` defaults to `src/Host/appsettings.json` and implement startup validation for item concurrency 1..16, positive endpoint permits, and non-negative queue length in `src/Host/Configuration/BestStoriesOptionsValidator.cs`
- [x] T012 Register options, `IBestStoriesService`, Problem Details, and the named ASP.NET Core concurrency-limiter policy in `src/Host/DependencyInjection/BestStoriesServiceCollectionExtensions.cs`
- [x] T013 Wire `AddBestStories`, `UseRateLimiter`, controller mapping, and a public partial `Program` test entry point without disturbing the 001 pipeline in `src/Host/Program.cs`
- [x] T014 Add composition tests for valid defaults and every invalid BestStories option boundary in `tests/IntegrationTests/BestStories/BestStoriesRegistrationTests.cs`

**Checkpoint**: The Host fails fast on invalid feature configuration, the service contracts
compile, and deterministic unit/HTTP fixtures are available without live network access.

---

## Phase 3: User Story 1 - Read the Best Stories (Priority: P1) MVP

**Goal**: Return up to `n` retrievable stories through the exact public JSON contract with
progressive bounded selection and deterministic score ordering.

**Independent Test**: A controlled feed containing complete, duplicate, missing, deleted,
dead, non-story, and incomplete items returns the first `n` retrievable distinct candidates
ordered by score and mapped to exactly the six public fields.

### Tests for User Story 1

> Write these tests first and verify they fail for missing feature behavior.

- [x] T015 [P] [US1] Add unit tests for first-occurrence deduplication, retrievability filtering, nullable URI, zero default comment count, and invalid Unix time in `tests/UnitTests/BestStories/BestStoriesSelectionTests.cs`
- [x] T016 [P] [US1] Add unit tests for descending scores, stable feed-position ties, at-most-`n` selection, empty/fewer-than-`n` feeds, and stopping before a later batch in `tests/UnitTests/BestStories/BestStoriesOrderingTests.cs`
- [x] T017 [P] [US1] Add HTTP contract tests for route, content type, exact camel-case properties, nullable `uri`, UTC ISO-8601 time, empty array, and absence of extra fields in `tests/IntegrationTests/BestStories/BestStoriesContractTests.cs`
- [x] T018 [P] [US1] Add controlled-upstream integration tests proving later candidates replace missing/deleted/dead/non-story/unmappable items in `tests/IntegrationTests/BestStories/BestStoriesProgressiveFetchTests.cs`

### Implementation for User Story 1

- [x] T019 [US1] Implement progressive distinct-ID batching, feed-position tracking, stop conditions, and cancellation checkpoints in `src/Application/BestStories/BestStoriesService.cs`
- [x] T020 [US1] Implement story retrievability rules, Unix-time conversion, nullable URL and comment defaults, selection by feed position, and deterministic score ordering in `src/Application/BestStories/BestStoriesService.cs`
- [x] T021 [US1] Implement `GET /api/v1/best-stories` success mapping and the six-field response contract in `src/Host/Controllers/BestStoriesController.cs`
- [x] T022 [US1] Describe the route, `n` range, nullable `uri`, success schema, and declared responses through controller/OpenAPI metadata in `src/Host/Controllers/BestStoriesController.cs`
- [x] T023 [US1] Update `src/Host/Api.http` with valid, lower-bound, upper-bound, and empty-result request examples
- [x] T024 [US1] Run the US1 unit and integration filters and record evidence for SC-001, SC-003, and SC-006 in `specs/002-best-stories-endpoint/checklists/requirements.md`

**Checkpoint**: The independently runnable MVP returns the correct public contract, selection,
and ordering through the real HTTP boundary.

---

## Phase 4: User Story 2 - Reject Unsafe Requests (Priority: P1)

**Goal**: Reject malformed, ambiguous, and excessive `n` values before any client work and
reject overload before application orchestration.

**Independent Test**: Missing, repeated, malformed, zero, negative, overflowing, and 101
values return safe `400` Problem Details with zero client calls; exhausted HTTP admission
returns `429` without invoking the use case.

### Tests for User Story 2

> Write these tests first and verify they fail for the intended validation/admission gap.

- [x] T025 [P] [US2] Add HTTP boundary tests for missing, empty, whitespace, repeated, non-integer, overflow, 0, -1, 100, and 101 query values with zero client calls on rejection in `tests/IntegrationTests/BestStories/BestStoriesValidationTests.cs`
- [x] T026 [P] [US2] Add an HTTP admission test that occupies 100 permits and 200 FIFO queue entries, verifies later requests receive `429`, and proves rejected requests start no use-case work in `tests/IntegrationTests/BestStories/BestStoriesAdmissionTests.cs`

### Implementation for User Story 2

- [x] T027 [US2] Bind and validate the raw single `n` query value before constructing `BestStoriesQuery`, returning safe RFC 9457 `400` responses in `src/Host/Controllers/BestStoriesController.cs`
- [x] T028 [US2] Attach the named concurrency-limiter policy and safe `429` rejection response to the endpoint in `src/Host/Controllers/BestStoriesController.cs` and `src/Host/DependencyInjection/BestStoriesServiceCollectionExtensions.cs`
- [x] T029 [US2] Run the US2 filters and record evidence for SC-002 and SC-007 in `specs/002-best-stories-endpoint/checklists/requirements.md`

**Checkpoint**: All input and endpoint admission bounds are independently verifiable before
upstream work begins.

---

## Phase 5: User Story 3 - Remain Correct Under Partial Data and Failure (Priority: P2)

**Goal**: Continue only for definitive non-retrievable candidates and otherwise return a
classified safe error instead of a misleading partial success.

**Independent Test**: Script every 001 outcome at feed and item boundaries and verify the
documented continuation or 400/502/503/504/cancellation behavior and absence of sensitive data.

### Tests for User Story 3

> Write these tests first and verify they fail for incomplete outcome translation.

- [x] T030 [P] [US3] Add application tests for missing and invalid-data skips plus terminal timeout, unavailable, throttled, and canceled item outcomes in `tests/UnitTests/BestStories/BestStoriesFailureTests.cs`
- [x] T031 [P] [US3] Add HTTP tests for invalid feed `502`, unavailable/throttled `503`, timeout `504`, safe Problem Details, and no partial array in `tests/IntegrationTests/BestStories/BestStoriesFailureMappingTests.cs`
- [x] T032 [P] [US3] Add cancellation tests for between-batch and in-flight disconnection behavior in `tests/IntegrationTests/BestStories/BestStoriesCancellationTests.cs`

### Implementation for User Story 3

- [x] T033 [US3] Complete closed feed/item outcome handling so only definitive non-retrievable candidates are skipped and terminal failures cancel remaining request-owned orchestration in `src/Application/BestStories/BestStoriesService.cs`
- [x] T034 [US3] Map application failures to safe RFC 9457 `502`, `503`, and `504` responses while leaving disconnected requests canceled in `src/Host/Controllers/BestStoriesController.cs`
- [x] T035 [US3] Run the US3 filters and record failure and cancellation evidence for SC-008 in `specs/002-best-stories-endpoint/checklists/requirements.md`

**Checkpoint**: Partial data and dependency failures cannot produce an apparently complete but
incorrect ranked success.

---

## Phase 6: User Story 4 - Reuse Upstream Work at High Load (Priority: P2)

**Goal**: Demonstrate bounded per-request and process-wide fan-out, same-key coalescing, and
zero extra upstream traffic on fresh cache hits across concurrent endpoint requests.

**Independent Test**: Run 100 concurrent identical cold HTTP requests and distinct-item load
against the controlled upstream, observing all application and 001 protection limits.

### Tests for User Story 4

> Write these tests first and verify they expose any unbounded or duplicate work.

- [x] T036 [P] [US4] Add application concurrency tests proving at most 8 item operations per request and no later batch after `n` is satisfied in `tests/UnitTests/BestStories/BestStoriesConcurrencyTests.cs`
- [x] T037 [P] [US4] Add 100-caller cold-key HTTP tests proving one logical population per feed/item key and zero physical calls on subsequent fresh-cache requests in `tests/IntegrationTests/BestStories/BestStoriesCoalescingTests.cs`
- [x] T038 [P] [US4] Add concurrent distinct-item HTTP tests proving the 001 process-wide limit of 16 covers all endpoint requests and retries in `tests/IntegrationTests/BestStories/BestStoriesGlobalConcurrencyTests.cs`
- [x] T039 [P] [US4] Add diagnostics tests for duration, requested/examined/returned/skipped counts, safe outcome dimensions, and admission rejection without payload/internal-URL logging in `tests/IntegrationTests/BestStories/BestStoriesDiagnosticsTests.cs`

### Implementation for User Story 4

- [x] T040 [US4] Add low-cardinality logs, counters, and duration instrumentation around orchestration outcomes in `src/Application/BestStories/BestStoriesService.Diagnostics.cs` and `src/Application/BestStories/BestStoriesService.cs`
- [x] T041 [US4] Run the load-protection filters and record evidence for SC-004 and SC-005 plus the upstream-protection gates in `specs/002-best-stories-endpoint/checklists/requirements.md`

**Checkpoint**: High concurrent request volume remains bounded at endpoint, per-request, cache,
and process-wide upstream layers without a final cache keyed by `n`.

---

## Phase 7: Polish & Cross-Cutting Verification

**Purpose**: Finish documentation, contract discovery, security review, and all delivery gates.

- [x] T042 [P] Document build, run, endpoint examples, assumptions, configuration, and realistic future enhancements in `README.md`
- [x] T043 [P] Add XML documentation and nullable-contract review for `src/Application/BestStories/*.cs` and `src/Host/Controllers/BestStoriesController.cs`
- [x] T044 Verify the generated OpenAPI document describes the route, `n` constraints, nullable `uri`, success array, and 400/429/502/503/504 responses in `tests/IntegrationTests/BestStories/BestStoriesOpenApiTests.cs`
- [x] T045 Review production code and tests to prove there is no final-response cache, live Hacker News test call, unbounded task creation, payload logging, or duplicate HTTP client in `src/`, `tests/UnitTests/BestStories/`, and `tests/IntegrationTests/BestStories/`
- [x] T046 Run restore, formatting verification, warning-clean build, all unit/integration tests, and every command/request in `specs/002-best-stories-endpoint/quickstart.md`; record final evidence in `specs/002-best-stories-endpoint/checklists/requirements.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Starts immediately.
- **Phase 2 (Foundational)**: Depends on Setup and blocks every user story.
- **Phase 3 (US1)**: Depends on Foundation and delivers the endpoint MVP.
- **Phase 4 (US2)**: Depends on the US1 controller route but is independently verified by
  validation/admission tests.
- **Phase 5 (US3)**: Depends on US1 orchestration and HTTP mapping.
- **Phase 6 (US4)**: Depends on US1 plus the admission and failure semantics from US2/US3.
- **Phase 7 (Polish)**: Depends on every selected story.

### User Story Dependency Graph

```text
Setup -> Foundation -> US1 endpoint MVP -> US2 validation/admission
                                  |-----> US3 failure semantics
                         US2 + US3 -----> US4 load protection -> Polish
```

### Within Each User Story

1. Add the listed tests and verify they fail for the intended missing behavior.
2. Implement application behavior before Host translation.
3. Run the story-specific unit and integration filters.
4. Record measurable evidence before advancing.

### Parallel Opportunities

- T002 and T003 can proceed independently after T001; T004 is a read-only verification.
- T005-T010 touch distinct production/test files and can proceed in parallel; T011-T014 then
  complete Host composition.
- US1 tests T015-T018 can be written in parallel before T019; T021 follows T019-T020.
- US2 tests T025-T026 can proceed in parallel.
- US3 tests T030-T032 can proceed in parallel.
- US4 tests T036-T039 can proceed in parallel after earlier story behavior is available.
- Polish documentation T042 and contract documentation T043 can proceed in parallel.

## Parallel Examples

### User Story 1

```text
T015: Application filtering/mapping tests
T016: Application ordering/stop-condition tests
T017: HTTP response-contract tests
T018: Controlled-upstream progressive-fetch tests
```

### User Story 4

```text
T036: Per-request concurrency tests
T037: Endpoint cache/coalescing tests
T038: Process-wide outbound concurrency tests
T039: Diagnostics safety tests
```

## Implementation Strategy

### MVP First

1. Complete Setup and Foundation.
2. Complete US1 through T024.
3. Stop and demonstrate the exact endpoint response with progressive bounded retrieval.

### Incremental Delivery

1. **US1**: Correct success contract and ranking.
2. **US2**: Safe input and admission bounds.
3. **US3**: Explicit partial-data and failure semantics.
4. **US4**: Measured concurrency, cache reuse, coalescing, and observability.
5. **Polish**: Documentation, OpenAPI, security review, and complete quality gates.

## Notes

- `[P]` never marks tasks that edit the same file or depend on unfinished output.
- Tests precede implementation and must fail for the expected reason.
- The 001 client remains the only Hacker News transport path and owns caching, resilience,
  retries, and the process-wide outbound concurrency gate.
- Do not introduce final-response caching, Redis, stale serving, background refresh, hedging,
  distributed locking, or live Hacker News test traffic.
- Preserve unrelated worktree changes and commit only coherent task groups when requested.
