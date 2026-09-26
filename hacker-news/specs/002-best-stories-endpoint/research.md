# Research: Best Stories GET Endpoint

## Candidate selection semantics

**Decision**: Treat `beststories` feed position as candidate priority, scan distinct IDs in
bounded batches until `n` retrievable stories are collected, then sort that selected set by
score descending with feed position as the stable tie-breaker.

**Rationale**: The official feed defines which stories are "best" while the coding test
requires descending score presentation. Hacker News does not publish a safe score threshold
that would prove an unseen candidate cannot outrank a seen one. Selecting by feed position
honors the source ranking and progressive scanning avoids fetching the whole feed for small
`n` while still replacing definitive non-retrievable entries.

**Alternatives considered**:

- Fetch every feed item and select the highest scores: rejected because it may cause hundreds
  of unnecessary calls for small `n` and changes "best" from the feed's ranking to score alone.
- Fetch only the first `n` IDs: rejected because missing/deleted/non-story items could return
  fewer stories even when later retrievable candidates exist.
- Stop individual tasks mid-batch as soon as `n` completes: rejected because completion order
  is nondeterministic and canceling shared client/cache work could harm coalesced callers.

## Public input bound

**Decision**: Accept one integer query value `n` from 1 through 100.

**Rationale**: A fixed upper bound makes worst-case response size and orchestration fan-out
explicit. One hundred is large enough for the exercise while preventing an arbitrary caller
from forcing evaluation of the entire feed. Changing the bound changes the public contract
and should be versioned rather than silently configured per environment.

**Alternatives considered**: Unbounded input violates the constitution; a configurable public
maximum could make the same API behave incompatibly across deployments.

## Bounded concurrency at two scopes

**Decision**: Use an application batch size of 8 and retain the 001 process-wide Hacker News
gate of 16 permits with 64 queued operations.

**Rationale**: The per-request bound prevents one large query from immediately occupying all
global permits. The process-wide gate remains the actual dependency protection across callers
and retries. Batching also gives a deterministic stop point before scheduling more work.

**Alternatives considered**: `Task.WhenAll` over the full feed creates excessive queued work;
only per-request throttling does not protect the dependency across concurrent requests.

## Inbound admission

**Decision**: Apply ASP.NET Core's built-in concurrency rate limiter to the endpoint with
100 executing permits and a FIFO queue of 200 by default.

**Rationale**: It places a hard bound on active/queued request work and supplies predictable
backpressure before application orchestration. It is the constitution's default mechanism
and needs no third-party dependency.

**Alternatives considered**: A fixed-window request rate can still allow too many long-running
requests simultaneously; an unbounded queue converts overload into memory and latency growth.

## Cache strategy

**Decision**: Add no result cache. Reuse the 001 HybridCache feed and per-item entries.

**Rationale**: Separate source entries maximize reuse across all values of `n`, coalesce cold
same-key loads within one instance, and retain the established freshness rules. A final cache
would create up to 100 overlapping arrays and would not remove the need for item caching.

**Alternatives considered**: Response caching keyed by `n` was rejected as redundant and as
the wrong primary protection mechanism; Redis and distributed locking lack a multi-instance
requirement.

## Failure and partial-item policy

**Decision**: Skip only candidates that are definitively non-retrievable or cannot be mapped.
Treat timeout, unavailable, throttled, and cancellation as terminal.

**Rationale**: A null/deleted/dead/non-story/malformed item cannot be placed in the public
contract, so scanning onward is correct. A transient failure does not prove an item is not
retrievable; substituting a lower feed candidate would misrepresent the requested ranking.

**Alternatives considered**: Returning partial success after transient failures was rejected
because ordinary JSON provides no trustworthy indication that higher-priority stories were
omitted.

