using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspireLocalAI;

/// <summary>
/// Pulls the models declared on an Ollama server that are not yet present in its local store.
/// </summary>
/// <remarks>
/// Uses <c>GET /api/tags</c> to list installed models and <c>POST /api/pull</c> (non-streaming) to fetch missing ones,
/// one at a time. Tags are compared with an optional <c>:latest</c> suffix ignored on both sides.
/// </remarks>
internal sealed class OllamaModelPuller
{
    private const string TagsPath = "api/tags";
    private const string PullPath = "api/pull";
    private const string LatestTag = ":latest";

    /// <summary>
    /// Pulls every tag in <paramref name="models"/> that the server does not already have.
    /// </summary>
    /// <param name="endpoint">Base URI of the Ollama server.</param>
    /// <param name="models">Declared model tags.</param>
    /// <param name="http">Client used for the requests.</param>
    /// <param name="reportStatus">Receives a progress message before and after each pull.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The server returned a non-success status.</exception>
    public static async Task PullMissingAsync(
        Uri endpoint,
        IReadOnlyList<string> models,
        HttpClient http,
        Action<string> reportStatus,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(reportStatus);

        // Tracks normalized tags that are present or already requested, so duplicates in `models` pull once.
        var available = await ListInstalledAsync(endpoint, http, ct).ConfigureAwait(false);

        foreach (var tag in models)
        {
            if (!available.Add(TrimLatest(tag)))
            {
                continue;
            }

            reportStatus($"Pulling {tag}");
            await PullAsync(endpoint, tag, http, ct).ConfigureAwait(false);
            reportStatus($"Pulled {tag}");
        }
    }

    private static async Task<HashSet<string>> ListInstalledAsync(Uri endpoint, HttpClient http, CancellationToken ct)
    {
        using var response = await http.GetAsync(new Uri(endpoint, TagsPath), ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "list models", ct).ConfigureAwait(false);

        await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false);

        var installed = new HashSet<string>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("models", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    installed.Add(TrimLatest(name.GetString()!));
                }
            }
        }

        return installed;
    }

    private static async Task PullAsync(Uri endpoint, string tag, HttpClient http, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new PullRequest(tag, Stream: false));
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await http.PostAsync(new Uri(endpoint, PullPath), content, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"pull {tag}", ct).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new InvalidOperationException(
            $"Ollama failed to {operation}: {(int)response.StatusCode} {response.ReasonPhrase}. {details}");
    }

    private static string TrimLatest(string tag) =>
        tag.EndsWith(LatestTag, StringComparison.Ordinal) ? tag[..^LatestTag.Length] : tag;

    private sealed record PullRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("stream")] bool Stream);
}
