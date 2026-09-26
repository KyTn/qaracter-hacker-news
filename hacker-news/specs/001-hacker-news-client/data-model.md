# Data Model: Hacker News Client

## BestStoryIds

Immutable ordered collection of positive 64-bit Hacker News item identifiers.

### Validation

- The upstream root value must be a JSON array.
- Every element must be a positive integer representable as a signed 64-bit value.
- Upstream order is preserved.
- Duplicate identifiers are preserved at this boundary; deduplication is an orchestration
  decision and must not silently change the source contract.

## HackerNewsItem

Application-owned immutable record containing the fields needed by later selection and
mapping logic.

| Field | Type | Rules |
|---|---|---|
| `Id` | positive 64-bit integer | Must equal the requested item identifier |
| `Type` | nullable string | Preserved for story validation |
| `Title` | nullable string | Absence remains distinct from empty text |
| `Url` | nullable string | Syntax validation belongs to public mapping |
| `By` | nullable string | Absence remains explicit |
| `UnixTime` | nullable 64-bit integer | Converted later at the public mapping boundary |
| `Score` | nullable 64-bit integer | Numeric value used later for ordering |
| `Descendants` | nullable 64-bit integer | Maps later to comment count |
| `IsDeleted` | boolean | Defaults false only when the wire field is absent |
| `IsDead` | boolean | Defaults false only when the wire field is absent |

The record has no JSON attributes or transport status information.

## CachedItemEnvelope

Infrastructure-owned immutable cache value that avoids ambiguous cached nulls.

| Field | Type | Rules |
|---|---|---|
| `Found` | boolean | `false` represents a negative-cache entry |
| `Item` | nullable `HackerNewsItem` | Required exactly when `Found` is true |

### Lifetime

- Found item: 5 minutes by default.
- Missing/null item: 30 seconds by default.
- No stale state transition exists; expiration causes a new population attempt.

## HackerNewsResult<T>

Application-owned discriminated result returned by the port.

| Outcome | Value allowed | Meaning |
|---|---:|---|
| `Success` | yes | Valid translated result |
| `NotFound` | no | Item endpoint returned a missing/null item |
| `InvalidInput` | no | Identifier rejected before upstream work |
| `InvalidUpstreamData` | no | Successful response violated the expected schema |
| `Timeout` | no | Configured dependency deadline elapsed |
| `Unavailable` | no | Transport, circuit, or terminal upstream failure |
| `Throttled` | no | Local outbound queue rejected the operation |
| `Canceled` | no | Caller cancellation won the operation |

The result may carry a stable internal reason code but never an exception or raw payload.

## HackerNewsOptions

Validated configuration bound at Host startup.

| Option | Default | Validation |
|---|---:|---|
| Base address | official HTTPS origin | Absolute HTTPS URI |
| Feed TTL | 30 seconds | Positive and bounded |
| Item TTL | 5 minutes | Positive and bounded |
| Missing TTL | 30 seconds | Positive and no greater than item TTL |
| Maximum payload | 1 MiB | Positive |
| Maximum cache-key length | 128 | Large enough for defined keys |
| Concurrency permits | 16 | Positive |
| Queue length | 64 | Non-negative and bounded |
| Attempt timeout | 2 seconds | Positive and below total timeout |
| Total timeout | 5 seconds | Positive |
| Retry count | 2 | Range 0–2 |
| Initial retry delay | 200 ms | Positive |

Circuit-breaker sampling, minimum throughput, failure ratio, and break duration are also
validated and externally configurable.

