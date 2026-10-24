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

    /// <summary>
    /// Checks that the options are usable and throws otherwise.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="Timeout"/> is zero, negative, or greater than 10 minutes.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="WarmupPrompt"/> is non-null but consists only of whitespace.
    /// </exception>
    public void Validate()
    {
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Timeout), Timeout, "Timeout must be greater than zero and at most 10 minutes.");
        }

        if (WarmupPrompt is not null && WarmupPrompt.Trim().Length == 0)
        {
            throw new ArgumentException("WarmupPrompt must not be whitespace-only.", nameof(WarmupPrompt));
        }
    }
}
