using System.Net;
using System.Text;

namespace PenguinPlank.UnitTests.Web;

/// <summary>
/// A hand-written <see cref="HttpMessageHandler"/> that records each request and answers from a
/// caller-supplied function, so the typed client is tested without a network.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<RecordedRequest, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!.AbsoluteUri,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
            body);
        Requests.Add(recorded);
        return _responder(recorded);
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    string Uri,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
