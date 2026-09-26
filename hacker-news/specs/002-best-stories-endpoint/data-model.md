# Data Model: Best Stories GET Endpoint

## BestStoriesQuery

| Field | Type | Rules |
|---|---|---|
| `Count` | `int` | Inclusive range 1..100 |

Construction validates the invariant before the use case can request the feed.

## BestStory

| Public field | Application type | Source and rule |
|---|---|---|
| `title` | non-empty `string` | `HackerNewsItem.Title`; required |
| `uri` | nullable `string` | `HackerNewsItem.Url`; absent remains null |
| `postedBy` | non-empty `string` | `HackerNewsItem.By`; required |
| `time` | `DateTimeOffset` | Unix seconds converted to UTC; must be representable |
| `score` | `long` | `HackerNewsItem.Score`; required |
| `commentCount` | `long` | `Descendants`; absent maps to zero |

The HTTP serializer emits exactly the six camel-case field names above. Internal selection
metadata is never serialized.

## RankedStory

Internal immutable pair of a `BestStory`, positive Hacker News item ID, and zero-based feed
position. Feed position drives selection and deterministic score ties; ID is a defensive final
tie component.

## CandidateEvaluation

Closed internal result:

- `Retrievable(RankedStory)`
- `Skipped(reason)` where reason is one of missing, duplicate, deleted, dead, non-story,
  invalid-data, or unmappable
- `TerminalFailure(kind)` where kind is timeout, unavailable, throttled, or canceled

Skip reasons are low-cardinality diagnostic values and never include payload content.

## BestStoriesResult

Closed application outcome:

- `Success(IReadOnlyList<BestStory>)`
- `InvalidInput`
- `InvalidUpstreamData` (feed-level)
- `Timeout`
- `Unavailable`
- `Throttled`
- `Canceled`

The Host maps these outcomes to HTTP. No exception, URL, response body, or Infrastructure DTO
crosses the application boundary.

## State transitions

```text
raw query -> rejected
          -> validated query -> feed failure -> terminal outcome
                             -> ordered distinct candidates
                             -> evaluate bounded batch -> terminal outcome
                                                       -> collect/skip
                             -> enough collected or feed exhausted
                             -> select by feed position
                             -> score-desc/feed-position-asc order
                             -> success
```

## Invariants

- A success contains between 0 and `Count` unique item IDs.
- Every success item came from the current feed result and passed retrievability rules.
- No later batch begins after a completed batch brings the collected count to `Count`.
- Public ordering is score descending; equal scores preserve feed position.
- No request owns more than the configured item-concurrency number of in-flight item calls.

