using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspireLocalAI;

/// <summary>
/// Verifies that a local model server is reachable and serves the models it was declared with.
/// </summary>
/// <remarks>
/// Ollama is probed via <c>GET /api/tags</c> (<c>models[].name</c>), vLLM via <c>GET /v1/models</c>
/// (<c>data[].id</c>). Ollama tags are compared with an optional <c>:latest</c> suffix ignored on both sides.
/// </remarks>
internal sealed class LocalModelHealthCheck : IHealthCheck
{
    private const string OllamaTagsPath = "/api/tags";
    private const string OllamaGeneratePath = "/api/generate";
    private const string OpenAiModelsPath = "/v1/models";
    private const string OpenAiCompletionsPath = "/v1/completions";
    private const string LatestTag = ":latest";

    private readonly Func<CancellationToken, Task<Uri?>> _endpointResolver;
    private readonly IReadOnlyList<string> _requiredModels;
    private readonly LocalAIHealthCheckOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _isOllama;
    private volatile bool _warmedUp;

    /// <summary>
    /// Initializes a new <see cref="LocalModelHealthCheck"/>.
    /// </summary>
    /// <param name="endpointResolver">Returns the server's base URI, or <see langword="null"/> while it is not allocated yet.</param>
    /// <param name="requiredModels">Model identifiers that must be served.</param>
    /// <param name="options">Probe timeout and missing-model policy.</param>
    /// <param name="httpClient">Client used for the probe request.</param>
    /// <param name="isOllama"><see langword="true"/> for Ollama (<c>/api/tags</c>), <see langword="false"/> for OpenAI-compatible servers such as vLLM (<c>/v1/models</c>).</param>
    public LocalModelHealthCheck(
        Func<CancellationToken, Task<Uri?>> endpointResolver,
        IReadOnlyList<string> requiredModels,
        LocalAIHealthCheckOptions options,
        HttpClient httpClient,
        bool isOllama)
    {
        ArgumentNullException.ThrowIfNull(endpointResolver);
        ArgumentNullException.ThrowIfNull(requiredModels);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        _endpointResolver = endpointResolver;
        _requiredModels = requiredModels;
        _options = options;
        _httpClient = httpClient;
        _isOllama = isOllama;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpointResolver(cancellationToken).ConfigureAwait(false);
        if (endpoint is null)
        {
            return HealthCheckResult.Unhealthy("endpoint not allocated");
        }

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCts.CancelAfter(_options.Timeout);

        try
        {
            var probeUri = new Uri(endpoint, _isOllama ? OllamaTagsPath : OpenAiModelsPath);
            using var response = await _httpClient.GetAsync(probeUri, probeCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var body = await response.Content.ReadAsStreamAsync(probeCts.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: probeCts.Token).ConfigureAwait(false);

            var loaded = ParseModelNames(document.RootElement);
            var data = new Dictionary<string, object> { ["models"] = loaded };

            var missing = _requiredModels.Where(required => !loaded.Any(name => SameModel(name, required))).ToList();
            if (_options.RequireAllModels && missing.Count > 0)
            {
                return HealthCheckResult.Degraded(
                    $"missing models: {string.Join(", ", missing)}",
                    data: data);
            }

            // Warm-up only once every declared model is present, so the probe never targets a model that is not loaded.
            if (_options.WarmupPrompt is not null && missing.Count == 0 && _requiredModels.Count > 0)
            {
                data["warmup"] = await WarmUpAsync(endpoint, _requiredModels[0], _options.WarmupPrompt, cancellationToken)
                    .ConfigureAwait(false);
            }

            return HealthCheckResult.Healthy(data: data);
        }
        catch (Exception ex) when (IsProbeFailure(ex, cancellationToken))
        {
            return HealthCheckResult.Unhealthy("model server probe failed", ex);
        }
    }

    /// <summary>
    /// Sends the warm-up prompt once per instance. Failures are reported in the returned status and never
    /// fail the check, so the next check run retries the warm-up.
    /// </summary>
    private async Task<string> WarmUpAsync(Uri endpoint, string model, string prompt, CancellationToken callerToken)
    {
        if (_warmedUp)
        {
            return "done";
        }

        using var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        warmupCts.CancelAfter(_options.Timeout);

        try
        {
            var path = _isOllama ? OllamaGeneratePath : OpenAiCompletionsPath;
            object payload = _isOllama
                ? new { model, prompt, stream = false, options = new { num_predict = 1 } }
                : new { model, prompt, max_tokens = 1 };

            using var content = JsonContent.Create(payload);
            using var response = await _httpClient.PostAsync(new Uri(endpoint, path), content, warmupCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            _warmedUp = true;
            return "done";
        }
        catch (Exception ex) when (!callerToken.IsCancellationRequested)
        {
            return $"failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Treats network failures, probe timeouts and malformed responses as unhealthy, but lets
    /// cancellation requested by the caller propagate.
    /// </summary>
    private static bool IsProbeFailure(Exception ex, CancellationToken callerToken) =>
        ex is HttpRequestException or JsonException
        || (ex is TaskCanceledException && !callerToken.IsCancellationRequested);

    private List<string> ParseModelNames(JsonElement root)
    {
        var (arrayProperty, nameProperty) = _isOllama ? ("models", "name") : ("data", "id");
        var names = new List<string>();

        if (root.TryGetProperty(arrayProperty, out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty(nameProperty, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && value.GetString() is { } name)
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    private bool SameModel(string loaded, string required)
    {
        if (!_isOllama)
        {
            return string.Equals(loaded, required, StringComparison.Ordinal);
        }

        return string.Equals(TrimLatest(loaded), TrimLatest(required), StringComparison.Ordinal);
    }

    private static string TrimLatest(string tag) =>
        tag.EndsWith(LatestTag, StringComparison.Ordinal) ? tag[..^LatestTag.Length] : tag;
}
