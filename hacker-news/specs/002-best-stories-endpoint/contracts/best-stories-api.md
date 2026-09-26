# Contract: Best Stories API

## Request

```http
GET /api/v1/best-stories?n=3
Accept: application/json
```

`n` is required exactly once and must be a base-10 integer from 1 through 100 inclusive.

## Successful response

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
[
  {
    "title": "A story title",
    "uri": "https://example.com/story",
    "postedBy": "alice",
    "time": "2026-09-26T15:30:00+00:00",
    "score": 321,
    "commentCount": 42
  }
]
```

- The body is always an array and may contain fewer than `n` entries, including none.
- `uri` is the only nullable field.
- Entries are ordered by `score` descending; equal scores retain feed order.
- No pagination, envelope, cache metadata, or Hacker News wire fields are exposed.

## Errors

All error bodies use `application/problem+json` and contain safe RFC 9457 Problem Details.
They may include a trace identifier but never stack traces, dependency URLs, or payloads.

| Status | Condition |
|---|---|
| `400 Bad Request` | Missing, repeated, malformed, or out-of-range `n` |
| `429 Too Many Requests` | Endpoint concurrency permits and bounded queue exhausted |
| `502 Bad Gateway` | The best-story feed is invalid and selection cannot begin |
| `503 Service Unavailable` | Hacker News is unavailable or 001 rejects dependency admission |
| `504 Gateway Timeout` | A required Hacker News operation reaches its deadline |

If the caller disconnects, request cancellation is propagated and no replacement error body
is required.

## Selection contract

1. Preserve the first occurrence of every positive feed ID.
2. Evaluate candidates by feed position in batches of at most 8 by default.
3. Skip definitive non-retrievable candidates and continue until `n` are collected or the
   feed ends.
4. Do not continue after transient dependency failure; do not return a misleading partial
   success.
5. Select at most the first `n` retrievable candidates, then sort that set by score descending
   and feed position ascending.

