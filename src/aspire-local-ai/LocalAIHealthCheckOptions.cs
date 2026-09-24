namespace AspireLocalAI;

/// <summary>
/// Tuning options for the model health check registered by
/// <see cref="LocalAIResourceBuilderExtensions.WithModelHealthCheck{T}"/>.
/// </summary>
public sealed class LocalAIHealthCheckOptions
{
    /// <summary>
    /// Maximum time to wait for a single probe request. Model listing on a cold Ollama instance can be slow.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// When <see langword="true"/> the check fails if any declared model is missing on the server.
    /// When <see langword="false"/> only endpoint reachability is verified. Default: <see langword="true"/>.
    /// </summary>
    public bool RequireAllModels { get; set; } = true;

    /// <summary>
    /// Optional warm-up prompt sent once after the models are present, so the first real request does not pay
    /// the model load cost. <see langword="null"/> disables warm-up.
    /// </summary>
    public string? WarmupPrompt { get; set; }
}
