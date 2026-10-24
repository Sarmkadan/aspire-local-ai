using System.Net;
using System.Text;

namespace AspireLocalAI.Tests;

/// <summary>
/// In-memory <see cref="HttpMessageHandler"/> that answers requests by absolute path without touching the network.
/// Unregistered paths answer <see cref="HttpStatusCode.NotFound"/>.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Json)> _responses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);

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
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        if (_failures.TryGetValue(path, out var failure))
        {
            return Task.FromException<HttpResponseMessage>(failure);
        }

        var (status, json) = _responses.TryGetValue(path, out var registered)
            ? registered
            : (HttpStatusCode.NotFound, "{}");

        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        });
    }
}
