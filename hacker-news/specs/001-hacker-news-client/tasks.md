# Tasks: Hacker News Client

**Input**: Design documents from `specs/001-hacker-news-client/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Automated tests are mandatory. All HTTP tests use a controlled handler and must
never contact the live Hacker News API.

**Organization**: Tasks are grouped by user story. User Story 1 delivers the candidate-feed
MVP; User Story 2 incrementally extends the same port with item details; User Story 3 adds
the complete resilience and classified-failure behavior.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no incomplete dependency.
- **[Story]**: Maps the task to US1, US2, or US3 from `spec.md`.
- Every task names the exact files it creates or modifies.

## Phase 1: Setup

**Purpose**: Prepare project references, dependencies, and deterministic test projects.

- [x] T001 Add Application reference to `src/Infrastructure/Infrastructure.csproj`, add Infrastructure and Application references to `src/Host/Api.csproj`, and verify dependency direction remains Host -> Infrastructure -> Application
- [x] T002 Add `Microsoft.Extensions.Caching.Hybrid`, `Microsoft.Extensions.Http.Resilience`, and HTTP/options dependencies to `src/Infrastructure/Infrastructure.csproj` using .NET 10-compatible versions
- [x] T003 [P] Create xUnit project `tests/UnitTests/UnitTests.csproj` with a reference to `src/Application/Application.csproj`
- [x] T004 [P] Create xUnit project `tests/IntegrationTests/IntegrationTests.csproj` with references to `src/Application/Application.csproj`, `src/Infrastructure/Infrastructure.csproj`, and `src/Host/Api.csproj`
- [x] T005 Add both test projects to `src/Host.slnx` and verify the solution discovers them

---

## Phase 2: Foundational

**Purpose**: Establish contracts, validated configuration, shared concurrency, and test infrastructure required by every story.

**CRITICAL**: No user-story implementation begins until this phase is complete.

- [x] T006 [P] Implement the closed outcome enum and generic immutable result type in `src/Application/HackerNews/HackerNewsResult.cs`
- [x] T007 [P] Implement validated Hacker News configuration defaults, cache limits, timeouts, retries, circuit settings, and concurrency limits in `src/Host/Configuration/HackerNewsOptions.cs`
- [x] T008 [P] Implement the singleton FIFO concurrency limiter and lease abstraction in `src/Infrastructure/HackerNews/HackerNewsConcurrencyGate.cs`
- [x] T009 [P] Create deterministic scripted HTTP handler support with request count, active-count, response sequence, delay, and cancellation controls in `tests/IntegrationTests/HackerNews/Support/ScriptedHttpMessageHandler.cs`
- [x] T010 [P] Add unit tests for result invariants and invalid construction in `tests/UnitTests/HackerNews/HackerNewsResultTests.cs`
- [x] T011 Add the `HackerNews` default configuration section documented by the plan to `src/Host/appsettings.json`
- [x] T012 Implement options binding with startup validation and register the shared gate and HybridCache limits in `src/Host/DependencyInjection/HackerNewsServiceCollectionExtensions.cs`
- [x] T013 Add DI composition tests for valid defaults, invalid base address, invalid TTL relationships, invalid timeout relationships, retry bounds, and concurrency bounds in `tests/IntegrationTests/HackerNews/HackerNewsRegistrationTests.cs`

**Checkpoint**: The solution restores and builds; shared configuration fails fast when invalid; no live upstream operation exists yet.

---

## Phase 3: User Story 1 - Retrieve Best Story Candidates (Priority: P1) MVP

**Goal**: Retrieve the ordered best-story identifier feed through an application-owned port with bounded, coalesced caching.

**Independent Test**: A scripted upstream returns valid, empty, duplicate, malformed, invalid-ID, and oversized feeds; the client preserves valid order, classifies invalid data, coalesces 100 cold callers, and makes zero calls on a fresh hit.

### Tests for User Story 1

> Write these tests first and verify they fail before implementation.

- [x] T014 [P] [US1] Add port contract tests for ordered, empty, and duplicate candidate feeds in `tests/IntegrationTests/HackerNews/BestStoryFeedContractTests.cs`
- [x] T015 [P] [US1] Add invalid-data tests for null root, non-array JSON, malformed JSON, zero/negative IDs, numeric overflow, and oversized bodies in `tests/IntegrationTests/HackerNews/BestStoryFeedValidationTests.cs`
- [x] T016 [P] [US1] Add cache tests for `hn:beststories`, fresh hits, configured expiry, and 100-caller cold-key coalescing in `tests/IntegrationTests/HackerNews/BestStoryFeedCacheTests.cs`

### Implementation for User Story 1

- [x] T017 [P] [US1] Define the initial candidate-feed port method in `src/Application/HackerNews/IHackerNewsClient.cs`
- [x] T018 [P] [US1] Implement the immutable candidate-feed cache value in `src/Infrastructure/HackerNews/BestStoryIdsCacheEntry.cs`
- [x] T019 [US1] Implement bounded streaming deserialization, schema validation, cache population, and candidate result mapping in `src/Infrastructure/HackerNews/HackerNewsClient.cs`
- [x] T020 [US1] Register the transient typed client through `AddHttpClient<IHackerNewsClient, HackerNewsClient>` without duplicate service registration in `src/Host/DependencyInjection/HackerNewsServiceCollectionExtensions.cs`
- [x] T021 [US1] Add operation outcome, duration, cache population, upstream-call, and throttling diagnostics without payload logging in `src/Infrastructure/HackerNews/HackerNewsClient.Diagnostics.cs`
- [x] T022 [US1] Run the US1 tests and record evidence for SC-001, SC-002, SC-003, and feed-related SC-008 in `specs/001-hacker-news-client/checklists/requirements.md`

**Checkpoint**: US1 independently retrieves and caches the best-story candidate feed; no item-detail support is required for this MVP checkpoint.

---

## Phase 4: User Story 2 - Retrieve a Story Detail (Priority: P1)

**Goal**: Extend the client with validated item retrieval while keeping wire DTOs private and preserving absent optional values.

**Independent Test**: A scripted upstream returns a complete story, optional-field omissions, non-story/deleted/dead items, null, 404, mismatched IDs, malformed data, and invalid caller IDs; the port returns the specified application-owned outcome and values.

### Tests for User Story 2

> Write these tests first and verify they fail before implementation.

- [x] T023 [P] [US2] Add contract tests for complete stories and absent optional values in `tests/IntegrationTests/HackerNews/HackerNewsItemContractTests.cs`
- [x] T024 [P] [US2] Add boundary tests for non-positive IDs, mismatched IDs, non-story/deleted/dead fields, malformed JSON, numeric overflow, and oversized bodies in `tests/IntegrationTests/HackerNews/HackerNewsItemValidationTests.cs`
- [x] T025 [P] [US2] Add cache tests for `hn:item:{id}`, independent item keys, found-item TTL, 404/null negative caching, negative TTL, fresh hits, and same-key coalescing in `tests/IntegrationTests/HackerNews/HackerNewsItemCacheTests.cs`

### Implementation for User Story 2

- [x] T026 [P] [US2] Add the immutable application-owned item model from `data-model.md` in `src/Application/HackerNews/HackerNewsItem.cs`
- [x] T027 [US2] Extend the port with `GetItemAsync` and its typed result in `src/Application/HackerNews/IHackerNewsClient.cs`
- [x] T028 [P] [US2] Implement the private wire DTO and source-generated JSON metadata in `src/Infrastructure/HackerNews/HackerNewsItemDto.cs` and `src/Infrastructure/HackerNews/HackerNewsJsonContext.cs`
- [x] T029 [P] [US2] Implement the found/missing cache envelope invariant in `src/Infrastructure/HackerNews/CachedItemEnvelope.cs`
- [x] T030 [US2] Implement identifier pre-validation, item request construction, bounded deserialization, DTO translation, ID consistency checks, and negative caching in `src/Infrastructure/HackerNews/HackerNewsClient.Items.cs`
- [x] T031 [US2] Run the US2 tests and record evidence for item-related SC-001, SC-003, SC-005, SC-007, and SC-008 in `specs/001-hacker-news-client/checklists/requirements.md`

**Checkpoint**: US1 and US2 both pass independently; Application consumers can retrieve candidates and item details without referencing Infrastructure wire types.

---

## Phase 5: User Story 3 - Remain Predictable During Upstream Failure (Priority: P2)

**Goal**: Bound every upstream operation and expose deterministic timeout, cancellation, retry, circuit, unavailable, and local-throttling outcomes.

**Independent Test**: The scripted handler delays, disconnects, returns retryable and permanent statuses, and measures attempts/concurrency; each scenario terminates within its configured bound and produces exactly the expected classified outcome.

### Tests for User Story 3

> Write these tests first and verify they fail before implementation.

- [x] T032 [P] [US3] Add retry eligibility and attempt-count tests for transport failures, 408, 429, 5xx, permanent 4xx, malformed success bodies, and missing items in `tests/IntegrationTests/HackerNews/HackerNewsRetryTests.cs`
- [x] T033 [P] [US3] Add attempt-timeout, total-timeout, caller-cancellation, and shared-population waiter cancellation tests in `tests/IntegrationTests/HackerNews/HackerNewsCancellationTests.cs`
- [x] T034 [P] [US3] Add circuit opening/recovery tests and terminal unavailable mapping tests in `tests/IntegrationTests/HackerNews/HackerNewsCircuitBreakerTests.cs`
- [x] T035 [P] [US3] Add 100-distinct-key concurrency tests proving the global permit bound, bounded queue rejection, zero upstream calls on rejection, and retry attempts remaining inside one lease in `tests/IntegrationTests/HackerNews/HackerNewsConcurrencyTests.cs`

### Implementation for User Story 3

- [x] T036 [US3] Configure total timeout, attempt timeout, bounded exponential-jitter retry predicates, and circuit breaker on the typed client in `src/Host/DependencyInjection/HackerNewsServiceCollectionExtensions.cs`
- [x] T037 [US3] Acquire the singleton concurrency lease inside each cache factory and around the complete resilient send operation in `src/Infrastructure/HackerNews/HackerNewsClient.cs` and `src/Infrastructure/HackerNews/HackerNewsClient.Items.cs`
- [x] T038 [US3] Map caller cancellation, resilience timeout, circuit rejection, transport failure, terminal HTTP status, and local admission rejection to closed outcomes in `src/Infrastructure/HackerNews/HackerNewsClient.Failures.cs`
- [x] T039 [US3] Complete structured logs and meters for attempt count, upstream latency, classified failures, circuit rejection, and concurrency throttling in `src/Infrastructure/HackerNews/HackerNewsClient.Diagnostics.cs`
- [x] T040 [US3] Run the US3 suite and record evidence for SC-004, SC-005, SC-006, SC-007, and failure-related SC-008 in `specs/001-hacker-news-client/checklists/requirements.md`

**Checkpoint**: All user stories and constitutional upstream-protection behaviors pass without contacting the live Hacker News API.

---

## Phase 6: Polish & Cross-Cutting Verification

**Purpose**: Validate the complete feature, documentation, security boundaries, and commands.

- [x] T041 [P] Add XML documentation and nullable-contract review for the public application port and models in `src/Application/HackerNews/IHackerNewsClient.cs`, `src/Application/HackerNews/HackerNewsItem.cs`, and `src/Application/HackerNews/HackerNewsResult.cs`
- [x] T042 [P] Add a log-capture test proving payloads, internal URLs, and exception details are absent in `tests/IntegrationTests/HackerNews/HackerNewsDiagnosticsTests.cs`
- [x] T043 Verify redirects cannot leave the configured origin and add coverage in `tests/IntegrationTests/HackerNews/HackerNewsSecurityTests.cs` and handler configuration in `src/Host/DependencyInjection/HackerNewsServiceCollectionExtensions.cs`
- [x] T044 Update build, test, configuration, assumptions, and future-enhancement instructions for this feature in `README.md`
- [x] T045 Run restore, formatting verification, warning-clean build, all tests, and every command in `specs/001-hacker-news-client/quickstart.md`; record final evidence in `specs/001-hacker-news-client/checklists/requirements.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2 and is the MVP.
- **Phase 4 (US2)**: Depends on Phase 2 and the incremental port/client established by US1.
- **Phase 5 (US3)**: Depends on US1 and US2 so it can verify identical failure semantics for both operations.
- **Phase 6 (Polish)**: Depends on all selected user stories.

### User Story Dependency Graph

```text
Setup -> Foundation -> US1 (candidate feed) -> US2 (item detail) -> US3 (resilience)
                                                            \-> Polish <-/
```

US2 is independently testable through `GetItemAsync`, although implementation follows US1
because both operations intentionally share one incremental port and typed client. US3 tests
both operations and therefore follows both P1 stories.

### Within Each User Story

1. Add tests and verify they fail for the expected missing behavior.
2. Add or extend application-owned models and port contracts.
3. Implement private Infrastructure types and adapter behavior.
4. Complete DI integration and diagnostics.
5. Run the story-specific suite and record evidence before advancing.

### Parallel Opportunities

- T003 and T004 can run in parallel after shared dependency decisions.
- T006–T010 can run in parallel; T011 can proceed alongside them.
- US1 test tasks T014–T016 can run in parallel; T017 and T018 can run in parallel.
- US2 test tasks T023–T025 can run in parallel; T026, T028, and T029 can run in parallel.
- US3 test tasks T032–T035 can run in parallel.
- Polish tasks T041 and T042 can run in parallel before final verification.

## Parallel Examples

### User Story 1

```text
T014: Candidate feed contract tests
T015: Candidate feed invalid-data tests
T016: Candidate feed cache/coalescing tests
```

### User Story 2

```text
T023: Story mapping contract tests
T024: Story boundary/validation tests
T025: Story and negative-cache tests
```

### User Story 3

```text
T032: Retry behavior tests
T033: Timeout and cancellation tests
T034: Circuit-breaker tests
T035: Global concurrency and backpressure tests
```

## Implementation Strategy

### MVP First

1. Complete Setup and Foundational phases.
2. Complete US1 and demonstrate ordered candidate retrieval, caching, and coalescing.
3. Stop and validate the MVP before extending the port.

### Incremental Delivery

1. **US1**: Candidate feed capability.
2. **US2**: Item details and negative caching.
3. **US3**: Complete resilience and overload semantics.
4. **Polish**: Documentation, security checks, and full quality gates.

## Notes

- Tests precede implementation and must fail for the intended reason.
- `[P]` never marks tasks that edit the same file or depend on unfinished output.
- No task adds a public endpoint, best-`n` orchestration, score ordering, Redis, stale serving,
  background refresh, distributed locking, or live Hacker News test traffic.
- Commit after each task or coherent task group when the worktree permits isolated commits.
