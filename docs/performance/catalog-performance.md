# Catalog performance and capacity verification (Phase A, requirement A9)

This document declares the Phase A planning envelope, latency targets, Reference_Environment,
dataset, workload, and measurement method, and explains how to run the harness.

## 1. System targets (planning envelope, requirement 11.1)

| Quantity | Target |
|---|---|
| Concurrent internal users | 10 |
| Product variants | 5,000 |
| `ProductPiece` records | 50,000 |
| Stock lines | 250,000 (documented target only; stock is a Phase B+ workflow and is not seeded or measured in Phase A) |

## 2. Latency targets (requirement 11.2)

Measured on the declared Reference_Environment, excluding media transfer and external network time:

- **Reads:** p95 latency under **500 ms**.
- **Mutations:** under **1 s**. The harness asserts this at the 95th percentile (the same statistic as reads) and also reports the maximum.

Performance and capacity validation of Phase B to Phase G workflows is deferred to later releases, and the harness introduces no thresholds beyond these two (requirement 11.4).

## 3. Reference_Environment

The Reference_Environment is the machine that runs the harness. The harness records the facts below
in every report it writes; copy the report's "Reference_Environment" block into the table in section 8
when you commit a verified run.

- **Server software:** the real SQL Server instance `localhost\SQLEXPRESS` (SQL Server Express, Windows authentication), with the schema built by the committed EF Core migrations.
- **API host:** the real ASP.NET Core application (`Program`) hosted in-process through `WebApplicationFactory`, in the `Production` environment, using the real cookie authentication, antiforgery, idempotency, ETag, and projection pipeline.
- **Recorded per run:** operating system, logical processor count, memory available to the runtime, .NET runtime, and the SQL Server `@@VERSION` line.

Because the API runs in-process, no network, TLS, or browser time is included. This matches the
"excluding media transfer and external network time" clause. The harness does not exercise media endpoints.

## 4. Dataset (requirement 11.3)

Seeded by set-based T-SQL (`CatalogSeedSql`) into a throw-away database created from the actual migrations:

| Entity | Count | Shape |
|---|---:|---|
| Products | 100 | 10 categories; Draft |
| Variants | 5,000 | 50 per product; alternating `Quantity` and `Serialized` tracking; unique SKU `PERF-nnnnnnnn` and barcode; dimensions, finish, retail and wholesale price |
| Pieces | 50,000 | 20 per serialized variant (2,500 serialized variants); unique piece code; dimensions, finish, story, status |

The small `CatalogDatasetSpec.Smoke` shape (10 products, 40 variants, 60 pieces) is used by the default test run to verify that the seed SQL still matches the schema.

Safety: the seeder runs only against the isolated database that `AcceptanceApiFactory` creates (`PenguinPlank_IntegrationTests_<guid>`) and drops. It also refuses to run when the target already contains products, so it cannot seed a populated database.

## 5. Workload

Ten simulated users sign in as Owner through the real `/auth/login` flow and run concurrently. They are released together from a start gate. Each user runs 40 iterations; each iteration is eight reads and one mutation (a 8:1 back-office mix, 320 reads and 40 mutations per user).

Reads, per iteration:

1. `GET /products` (random page, size 50)
2. `GET /products/{id}`
3. `GET /variants` (random page across the 5,000 variants)
4. `GET /variants?productId=`
5. `GET /variants?search=PERF-nnnn`
6. `GET /variants/{id}`
7. `GET /pieces?variantId=`
8. `GET /pieces` (random page across the 50,000 pieces)

Mutations, one per iteration, rotating: `POST /products`, `POST /variants`, `POST /pieces`, `PATCH /variants/{id}`, `PATCH /products/{id}`. Every mutation carries the CSRF token and a fresh `Idempotency-Key`; edits send `If-Match` taken from a preceding `GET` (that `GET` is counted as a read).

Each user edits only its own slice of the seeded records, so concurrent users never collide on a row. Optimistic-concurrency conflicts have their own tests and are not part of the latency measurement. Randomness is seeded (`20260101 + userIndex`), so the request sequence is repeatable.

Before the measured window each user runs 5 unmeasured warm-up iterations (JIT, connection pool, EF caches). Login throttling (`RateLimiting:LoginPermitLimit`) is lifted for the harness only, so ten sessions can sign in from one address; catalog endpoints are unaffected.

## 6. Measurement method

- Latency is the wall-clock time from sending a request to having read the complete response body, taken with `TimeProvider.GetTimestamp` / `GetElapsedTime`.
- Percentiles use the **nearest-rank** method (`LatencyStatistics.Percentile`): the sample at rank `ceil(p/100 * n)` of the sorted samples. The value is always an observed latency. The function is pure and unit tested.
- Only successful requests (the expected status code) contribute latencies. Any request that returns an unexpected status fails the run, so an error response cannot pass as a fast one.
- A run that observes no reads or no mutations fails.
- The test asserts read p95 < 500 ms and mutation p95 < 1 s and writes a Markdown report (environment, dataset, per-operation n/mean/p50/p95/p99/max, verdicts).

## 7. Running the harness

The performance suite is opt-in and excluded from the default run: the test is skipped unless
`PENGUINPLANK_RUN_PERFORMANCE=1`, and it carries the `Category=Performance` trait.

```powershell
# Default fast run (performance test is skipped; seed-SQL and statistics tests still run)
dotnet test tests/IntegrationTests

# Exclude the trait explicitly
dotnet test tests/IntegrationTests --filter "Category!=Performance"

# Opt in (full reference dataset)
$env:PENGUINPLANK_RUN_PERFORMANCE = "1"
dotnet test tests/IntegrationTests -c Release --filter "Category=Performance" --logger "console;verbosity=detailed"
```

Optional settings: `PENGUINPLANK_PERF_SCALE=smoke` (small dataset, for a quick check; not valid evidence),
`PENGUINPLANK_PERF_ITERATIONS` (default 40), `PENGUINPLANK_PERF_REPORT_DIR` (default: the temp directory).

Run in `Release`, on an otherwise idle machine, with SQL Server running locally. CI jobs opt in by
setting the environment variable; the default CI integration job does not.

## 8. Verified runs

Record each verified run here: date, commit, the Reference_Environment block from the report, and the read and mutation p95 results.

| Date | Build | Reference_Environment | Read p95 | Mutation p95 | Result |
|---|---|---|---:|---:|---|
| 2026-11-17 | Debug, task 17.5 harness | Windows 10.0.19045; 8 logical processors; 63.9 GiB; .NET 10.0.12; SQL Server 2025 (RTM) 17.0.1000.7 Express, local | 253.0 ms (n=3,360) | 934.4 ms (n=400) | PASS (0 failed requests) |

Observations from the 2026-11-17 run. These are not new thresholds (requirement 11.4); they show where the margin is thin:

- Mutation p95 (934 ms) is close to the 1 s target. `POST /pieces` is the slowest mutation (p95 1,253 ms, max 1,865 ms), followed by `POST /variants` (p95 990 ms).
- Unfiltered `GET /pieces` paging across 50,000 pieces has p95 820 ms and a p99 of about 14.6 s. `GET /variants` paging has a similar p99 outlier (about 14.4 s). Filtered and by-id reads are all under 60 ms at p95.
- The run used a Debug build. A Release run is expected to be faster and is the preferred evidence.
