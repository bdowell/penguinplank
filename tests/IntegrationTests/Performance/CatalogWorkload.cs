using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PenguinPlank.Contracts.Catalog;

namespace IntegrationTests.Performance;

/// <summary>
/// The repeatable per-user catalog workload (requirement A9 §11.3). Each iteration issues eight
/// reads and one mutation, rotating through five mutation shapes, so a run is 8:1 read-heavy like
/// an internal back-office session. All randomness comes from the injected <see cref="Random"/>,
/// so a fixed seed reproduces the same request sequence.
/// </summary>
/// <remarks>
/// Each simulated user edits only its own slice of the seeded records so concurrent users never
/// contend for the same row; optimistic-concurrency conflicts are an application behaviour with
/// its own tests, not part of the latency measurement. Timing starts when the request is sent and
/// stops after the full response body has been read; it therefore includes ASP.NET Core
/// processing, authorization, serialization and SQL Server work, and excludes browser, TLS and
/// network time (requirement 11.2).
/// </remarks>
internal sealed class CatalogWorkload
{
    /// <summary>The number of reads issued per iteration.</summary>
    public const int ReadsPerIteration = 8;

    private const string CsrfHeaderName = "X-CSRF-TOKEN";
    private const string IdempotencyHeaderName = "Idempotency-Key";
    private const int PageSize = 50;
    private const int MutationShapeCount = 5;

    private readonly HttpClient _client;
    private readonly string _csrfToken;
    private readonly LatencyRecorder _recorder;
    private readonly Random _random;
    private readonly int _userIndex;
    private readonly string _runId;
    private readonly CatalogDatasetSpec _spec;
    private readonly ConcurrentQueue<string> _failures;
    private readonly List<Guid> _productIds;
    private readonly List<Guid> _variantIds;
    private readonly List<Guid> _serializedVariantIds;

    public CatalogWorkload(
        HttpClient client,
        string csrfToken,
        LatencyRecorder recorder,
        Random random,
        int userIndex,
        int userCount,
        string runId,
        CatalogDatasetSpec spec,
        CatalogDatasetSample sample,
        ConcurrentQueue<string> failures)
    {
        _client = client;
        _csrfToken = csrfToken;
        _recorder = recorder;
        _random = random;
        _userIndex = userIndex;
        _runId = runId;
        _spec = spec;
        _failures = failures;
        _productIds = Slice(sample.ProductIds, userIndex, userCount);
        _variantIds = Slice(sample.VariantIds, userIndex, userCount);
        _serializedVariantIds = Slice(sample.SerializedVariantIds, userIndex, userCount);
    }

    /// <summary>Runs one workload iteration: eight reads, then one mutation.</summary>
    /// <param name="iteration">The zero-based iteration number for this user.</param>
    /// <returns>A task that completes when the iteration has been recorded.</returns>
    public async Task RunIterationAsync(int iteration)
    {
        await ReadAsync("GET /products (page)", $"/api/v1/products?page={RandomPage(_spec.ProductCount)}&pageSize={PageSize}").ConfigureAwait(false);
        await ReadAsync("GET /products/{id}", $"/api/v1/products/{Pick(_productIds)}").ConfigureAwait(false);
        await ReadAsync("GET /variants (page)", $"/api/v1/variants?page={RandomPage(_spec.VariantCount)}&pageSize={PageSize}").ConfigureAwait(false);
        await ReadAsync("GET /variants?productId", $"/api/v1/variants?productId={Pick(_productIds)}&pageSize={PageSize}").ConfigureAwait(false);
        await ReadAsync("GET /variants?search", $"/api/v1/variants?search={CatalogDatasetSpec.SkuPrefix}{_random.Next(0, Math.Max(1, _spec.VariantCount)):D4}&pageSize={PageSize}").ConfigureAwait(false);
        await ReadAsync("GET /variants/{id}", $"/api/v1/variants/{Pick(_variantIds)}").ConfigureAwait(false);
        await ReadAsync("GET /pieces?variantId", $"/api/v1/pieces?variantId={Pick(_serializedVariantIds)}&pageSize={PageSize}").ConfigureAwait(false);
        await ReadAsync("GET /pieces (page)", $"/api/v1/pieces?page={RandomPage(_spec.PieceCount)}&pageSize={PageSize}").ConfigureAwait(false);

        switch (iteration % MutationShapeCount)
        {
            case 0:
                await CreateProductAsync(iteration).ConfigureAwait(false);
                break;
            case 1:
                await CreateVariantAsync(iteration).ConfigureAwait(false);
                break;
            case 2:
                await CreatePieceAsync(iteration).ConfigureAwait(false);
                break;
            case 3:
                await UpdateVariantAsync().ConfigureAwait(false);
                break;
            default:
                await UpdateProductAsync(iteration).ConfigureAwait(false);
                break;
        }
    }

    private static List<Guid> Slice(IReadOnlyList<Guid> ids, int userIndex, int userCount)
    {
        List<Guid> slice = [.. ids.Where((_, index) => index % userCount == userIndex)];
        if (slice.Count == 0)
        {
            // Sharing records between users would turn the latency run into a concurrency-conflict test.
            throw new InvalidOperationException(
                $"The dataset has {ids.Count} record(s) of a kind but {userCount} users; every user needs its own slice.");
        }

        return slice;
    }

    private Guid Pick(List<Guid> pool) => pool[_random.Next(pool.Count)];

    private int RandomPage(int totalRows) => _random.Next(1, Math.Max(1, (totalRows + PageSize - 1) / PageSize) + 1);

    private Task ReadAsync(string name, string url) =>
        _recorder.MeasureAsync(name, OperationKind.Read, async () =>
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            (bool ok, _) = await SendAsync(name, request, HttpStatusCode.OK).ConfigureAwait(false);
            return ok;
        });

    private async Task CreateProductAsync(int iteration)
    {
        CreateProductContract body = new()
        {
            Name = $"Perf run {_runId} product u{_userIndex} i{iteration}",
            Category = "Performance",
            PublicDescription = "Created by the performance harness.",
            InternalNotes = "Harness-created.",
        };

        Guid? id = await CreateAsync("POST /products", "/api/v1/products", body).ConfigureAwait(false);
        if (id is Guid created)
        {
            _productIds.Add(created);
        }
    }

    private async Task CreateVariantAsync(int iteration)
    {
        CreateVariantContract body = new()
        {
            ProductId = Pick(_productIds),
            Sku = $"PERFRUN-{_runId}-{_userIndex}-{iteration}",
            TrackingMode = "Quantity",
            UnitOfMeasure = "each",
            Dimensions = new DimensionsContract { Length = 12m, Width = 6m, Thickness = 0.75m, Unit = "in" },
            Finish = "Natural oil",
            RetailPrice = 30m,
            WholesalePrice = 15m,
            CasePack = 6,
        };

        Guid? id = await CreateAsync("POST /variants", "/api/v1/variants", body).ConfigureAwait(false);
        if (id is Guid created)
        {
            _variantIds.Add(created);
        }
    }

    private async Task CreatePieceAsync(int iteration)
    {
        CreatePieceContract body = new()
        {
            VariantId = Pick(_serializedVariantIds),
            PieceCode = $"PERFRUN-P-{_runId}-{_userIndex}-{iteration}",
            Dimensions = new DimensionsContract { Length = 12m, Width = 6m, Thickness = 0.75m, Unit = "in" },
            Finish = "Natural oil",
            Story = "Created by the performance harness.",
            Status = "InStock",
        };

        await CreateAsync("POST /pieces", "/api/v1/pieces", body).ConfigureAwait(false);
    }

    private async Task UpdateVariantAsync()
    {
        Guid id = Pick(_variantIds);
        string? eTag = await FetchETagAsync("GET /variants/{id} (for edit)", $"/api/v1/variants/{id}").ConfigureAwait(false);
        if (eTag is null)
        {
            return;
        }

        UpdateVariantContract body = new()
        {
            UnitOfMeasure = "each",
            Dimensions = new DimensionsContract { Length = 13m, Width = 6m, Thickness = 0.75m, Unit = "in" },
            Finish = "Natural oil and wax",
            RetailPrice = 32m,
            WholesalePrice = 16m,
        };

        await _recorder.MeasureAsync("PATCH /variants/{id}", OperationKind.Mutation, async () =>
        {
            using HttpRequestMessage request = BuildMutation(HttpMethod.Patch, $"/api/v1/variants/{id}", body, eTag);
            (bool ok, _) = await SendAsync("PATCH /variants/{id}", request, HttpStatusCode.NoContent).ConfigureAwait(false);
            return ok;
        }).ConfigureAwait(false);
    }

    private async Task UpdateProductAsync(int iteration)
    {
        Guid id = Pick(_productIds);
        string? eTag = await FetchETagAsync("GET /products/{id} (for edit)", $"/api/v1/products/{id}").ConfigureAwait(false);
        if (eTag is null)
        {
            return;
        }

        UpdateProductContract body = new()
        {
            Name = $"Perf run {_runId} renamed u{_userIndex} i{iteration}",
            Category = "Performance",
            PublicDescription = "Edited by the performance harness.",
            InternalNotes = "Harness-edited.",
        };

        await _recorder.MeasureAsync("PATCH /products/{id}", OperationKind.Mutation, async () =>
        {
            using HttpRequestMessage request = BuildMutation(HttpMethod.Patch, $"/api/v1/products/{id}", body, eTag);
            (bool ok, _) = await SendAsync("PATCH /products/{id}", request, HttpStatusCode.NoContent).ConfigureAwait(false);
            return ok;
        }).ConfigureAwait(false);
    }

    private async Task<string?> FetchETagAsync(string name, string url)
    {
        string? eTag = null;
        await _recorder.MeasureAsync(name, OperationKind.Read, async () =>
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            (bool ok, string? header) = await SendAsync(name, request, HttpStatusCode.OK).ConfigureAwait(false);
            if (ok && string.IsNullOrEmpty(header))
            {
                _failures.Enqueue($"{name}: the 200 response carried no ETag header.");
                return false;
            }

            eTag = header;
            return ok;
        }).ConfigureAwait(false);
        return eTag;
    }

    private async Task<Guid?> CreateAsync<TBody>(string name, string url, TBody body)
    {
        Guid? created = null;
        await _recorder.MeasureAsync(name, OperationKind.Mutation, async () =>
        {
            using HttpRequestMessage request = BuildMutation(HttpMethod.Post, url, body, ifMatch: null);
            (bool ok, string? responseBody) = await SendWithBodyAsync(name, request, HttpStatusCode.Created).ConfigureAwait(false);
            if (ok && responseBody is not null)
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                created = document.RootElement.GetProperty("id").GetGuid();
            }

            return ok;
        }).ConfigureAwait(false);
        return created;
    }

    private HttpRequestMessage BuildMutation<TBody>(HttpMethod method, string url, TBody body, string? ifMatch)
    {
        HttpRequestMessage request = new(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add(CsrfHeaderName, _csrfToken);
        request.Headers.Add(IdempotencyHeaderName, Guid.NewGuid().ToString("N"));
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }

        return request;
    }

    private async Task<(bool Ok, string? ETag)> SendAsync(string name, HttpRequestMessage request, HttpStatusCode expected)
    {
        using HttpResponseMessage response = await _client.SendAsync(request).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        // Read the raw header: the typed HttpResponseHeaders.ETag accessor returns null for an opaque
        // token it cannot parse, while the API only promises a quoted opaque value.
        string? eTag = response.Headers.TryGetValues("ETag", out IEnumerable<string>? values)
            ? values.FirstOrDefault()?.Trim().Trim('"')
            : null;
        return (Check(name, response.StatusCode, expected, body), eTag);
    }

    private async Task<(bool Ok, string? Body)> SendWithBodyAsync(string name, HttpRequestMessage request, HttpStatusCode expected)
    {
        using HttpResponseMessage response = await _client.SendAsync(request).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return (Check(name, response.StatusCode, expected, body), body);
    }

    private bool Check(string name, HttpStatusCode actual, HttpStatusCode expected, string body)
    {
        if (actual == expected)
        {
            return true;
        }

        _failures.Enqueue($"{name}: expected {(int)expected} but received {(int)actual}: {Truncate(body)}");
        return false;
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];
}
