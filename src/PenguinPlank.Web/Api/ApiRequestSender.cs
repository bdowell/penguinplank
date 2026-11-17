using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PenguinPlank.Contracts.Authentication;

namespace PenguinPlank.Web.Api;

/// <summary>
/// Sends same-origin requests to <c>/api/v1</c> and maps every outcome to a typed result.
/// </summary>
/// <remarks>
/// <para>
/// Authentication relies on the HttpOnly session cookie the browser attaches to same-origin
/// requests; this class never reads, stores, or sends a bearer token (requirement A5 7.2). Every
/// non-GET request carries the CSRF request token in the header the server names, fetched once
/// from <c>GET /api/v1/auth/csrf</c> and cached until <see cref="InvalidateCsrfToken"/> (requirement
/// A5 7.2). Mutations carry an <c>Idempotency-Key</c>, and edits carry the record's ETag in
/// <c>If-Match</c> (requirements A4 6.2, 6.5).
/// </para>
/// <para>
/// A request is sent exactly once. A lost connection yields <see cref="ApiErrorKind.Connectivity"/>
/// and the mutation is not queued or retried; the caller decides whether to resubmit, reusing the
/// same idempotency key so a request that did reach the server is not applied twice (requirement
/// A5 7.7). The instance caches the CSRF token, so register it scoped.
/// </para>
/// </remarks>
public sealed class ApiRequestSender
{
    /// <summary>The header carrying the idempotency key on a mutating request.</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>The same-origin route returning the CSRF request token.</summary>
    public const string CsrfPath = "api/v1/auth/csrf";

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IIdempotencyKeyGenerator _idempotencyKeys;
    private AntiforgeryTokenResponse? _csrfToken;

    /// <summary>
    /// Initializes the sender.
    /// </summary>
    /// <param name="httpClient">The same-origin HTTP client whose base address is the API origin.</param>
    /// <param name="idempotencyKeys">The source of idempotency keys for mutations.</param>
    /// <exception cref="ArgumentNullException">Thrown when a dependency is <see langword="null"/>.</exception>
    public ApiRequestSender(HttpClient httpClient, IIdempotencyKeyGenerator idempotencyKeys)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(idempotencyKeys);
        _httpClient = httpClient;
        _idempotencyKeys = idempotencyKeys;
    }

    /// <summary>Forgets the cached CSRF token so the next mutation fetches a fresh one.</summary>
    public void InvalidateCsrfToken() => _csrfToken = null;

    /// <summary>Sends a <c>GET</c> and reads the JSON body.</summary>
    /// <typeparam name="T">The response DTO type.</typeparam>
    /// <param name="path">The relative path and query, for example <c>api/v1/products</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed result.</returns>
    public async Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        RawResponse raw = await ExecuteAsync(HttpMethod.Get, path, content: null, ifMatch: null, idempotencyKey: null, cancellationToken)
            .ConfigureAwait(false);
        return ReadBody<T>(raw);
    }

    /// <summary>Sends a mutating request with a JSON body and reads the JSON response body.</summary>
    /// <typeparam name="TBody">The request DTO type.</typeparam>
    /// <typeparam name="TResponse">The response DTO type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="body">The request body.</param>
    /// <param name="ifMatch">The record's ETag token for an edit; <see langword="null"/> for a create.</param>
    /// <param name="idempotencyKey">The key to send; a new one is generated when <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed result.</returns>
    public async Task<ApiResult<TResponse>> SendAsync<TBody, TResponse>(
        HttpMethod method,
        string path,
        TBody body,
        string? ifMatch,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        RawResponse raw = await ExecuteAsync(method, path, JsonContent.Create(body, options: s_jsonOptions), ifMatch, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        return ReadBody<TResponse>(raw);
    }

    /// <summary>Sends a mutating request with a JSON body and no response body.</summary>
    /// <typeparam name="TBody">The request DTO type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="body">The request body.</param>
    /// <param name="ifMatch">The record's ETag token for an edit; <see langword="null"/> for a create.</param>
    /// <param name="idempotencyKey">The key to send; a new one is generated when <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed result.</returns>
    public async Task<ApiResult> SendAsync<TBody>(
        HttpMethod method,
        string path,
        TBody body,
        string? ifMatch,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        RawResponse raw = await ExecuteAsync(method, path, JsonContent.Create(body, options: s_jsonOptions), ifMatch, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        return raw.Error is null ? ApiResult.Success() : ApiResult.Failure(raw.Error);
    }

    /// <summary>Sends a mutating request with no body (for example logout).</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed result.</returns>
    public async Task<ApiResult> SendAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        RawResponse raw = await ExecuteAsync(method, path, content: null, ifMatch: null, idempotencyKey: null, cancellationToken)
            .ConfigureAwait(false);
        return raw.Error is null ? ApiResult.Success() : ApiResult.Failure(raw.Error);
    }

    private static ApiResult<T> ReadBody<T>(RawResponse raw)
    {
        if (raw.Error is not null)
        {
            return ApiResult.Failure<T>(raw.Error);
        }

        try
        {
            T? value = string.IsNullOrWhiteSpace(raw.Content)
                ? default
                : JsonSerializer.Deserialize<T>(raw.Content, s_jsonOptions);
            return value is null
                ? ApiResult.Failure<T>(ApiErrorMapper.UnreadableResponse(raw.StatusCode))
                : ApiResult.Success(value);
        }
        catch (JsonException)
        {
            return ApiResult.Failure<T>(ApiErrorMapper.UnreadableResponse(raw.StatusCode));
        }
    }

    private static string Quote(string token)
    {
        string trimmed = token.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A non-empty ETag token is required for an edit.", nameof(token));
        }

        return $"\"{trimmed}\"";
    }

    private async Task<RawResponse> ExecuteAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        string? ifMatch,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        bool isMutation = method != HttpMethod.Get;
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };

        if (isMutation)
        {
            ApiResult<AntiforgeryTokenResponse> csrf = await GetCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
            if (csrf.Error is not null)
            {
                return RawResponse.Failed(csrf.Error);
            }

            AntiforgeryTokenResponse token = csrf.Value!;
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
            request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader, idempotencyKey ?? _idempotencyKeys.NewKey());
        }

        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(Quote(ifMatch)));
        }

        try
        {
            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            int status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return new RawResponse(status, text, Error: null);
            }

            if (isMutation)
            {
                // A rejected mutation may mean the antiforgery token went stale; fetch a new one next time.
                InvalidateCsrfToken();
            }

            return RawResponse.Failed(ApiErrorMapper.FromResponse(status, text));
        }
        catch (HttpRequestException)
        {
            return RawResponse.Failed(ApiErrorMapper.Connectivity());
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The HTTP client timed out; the caller did not cancel. Treat as lost connectivity.
            return RawResponse.Failed(ApiErrorMapper.Connectivity());
        }
    }

    private async Task<ApiResult<AntiforgeryTokenResponse>> GetCsrfTokenAsync(CancellationToken cancellationToken)
    {
        AntiforgeryTokenResponse? cached = _csrfToken;
        if (cached is not null)
        {
            return ApiResult.Success(cached);
        }

        ApiResult<AntiforgeryTokenResponse> fetched = await GetAsync<AntiforgeryTokenResponse>(CsrfPath, cancellationToken)
            .ConfigureAwait(false);
        if (fetched.IsSuccess)
        {
            _csrfToken = fetched.Value;
        }

        return fetched;
    }

    private sealed record RawResponse(int StatusCode, string Content, ApiError? Error)
    {
        public static RawResponse Failed(ApiError error) => new(error.StatusCode ?? (int)HttpStatusCode.OK, string.Empty, error);
    }
}
