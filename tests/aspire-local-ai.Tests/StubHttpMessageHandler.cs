using System.Net;
using System.Text;

namespace AspireLocalAI.Tests;

/// <summary>
/// In-memory <see cref="HttpMessageHandler"/> that answers requests by absolute path without touching the network.
/// Unregistered paths answer <see cref="HttpStatusCode.NotFound"/>. Every request is recorded in <see cref="Requests"/>.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Json)> _responses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);

    /// <summary>Requests received so far, in arrival order. Requests rejected by cancellation are not recorded.</summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Registers a JSON response for the given request path.</summary>
    public StubHttpMessageHandler Respond(string path, HttpStatusCode status, string json)
    {
        _responses[path] = (status, json);
        return this;
    }

    /// <summary>Makes requests to the given path fail with <paramref name="exception"/>.</summary>
    public StubHttpMessageHandler Throw(string path, Exception exception)
    {
        _failures[path] = exception;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Mirror HttpClient: a request whose token is already cancelled never reaches the server.
        cancellationToken.ThrowIfCancellationRequested();

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add(new RecordedRequest(request.Method, path, body));

        if (_failures.TryGetValue(path, out var failure))
        {
            throw failure;
        }

        var (status, json) = _responses.TryGetValue(path, out var registered)
            ? registered
            : (HttpStatusCode.NotFound, "{}");

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
    }

    /// <summary>A request as seen by the stub: method, absolute path and body text (null when there is no body).</summary>
    public sealed record RecordedRequest(HttpMethod Method, string Path, string? Body);
}
