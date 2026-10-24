namespace AspireLocalAI;

/// <summary>
/// Options for <see cref="LocalAIResourceBuilderExtensions.WithOpenTelemetry{T}(Aspire.Hosting.ApplicationModel.IResourceBuilder{T}, LocalAIOpenTelemetryOptions)"/>
/// controlling the OpenTelemetry identity and signals a local model server exports.
/// </summary>
public sealed class LocalAIOpenTelemetryOptions
{
    /// <summary>
    /// Value for <c>OTEL_SERVICE_NAME</c>. When <see langword="null"/>, the Aspire resource name is used.
    /// Default: <see langword="null"/>.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// When <see langword="false"/>, trace export is turned off (<c>OTEL_TRACES_EXPORTER=none</c>) and vLLM
    /// is not asked for its per-request trace endpoint. Default: <see langword="true"/>.
    /// </summary>
    public bool EnableTraces { get; set; } = true;

    /// <summary>
    /// When <see langword="false"/>, metric export is turned off (<c>OTEL_METRICS_EXPORTER=none</c>).
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Extra resource attributes, written to <c>OTEL_RESOURCE_ATTRIBUTES</c> as comma-separated <c>key=value</c> pairs
    /// in insertion order. Keys are compared ordinally.
    /// </summary>
    public IDictionary<string, string> ResourceAttributes { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Checks that the options are usable and throws otherwise.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="ServiceName"/> is non-null but empty or whitespace, or a key in <see cref="ResourceAttributes"/>
    /// is empty or whitespace.
    /// </exception>
    public void Validate()
    {
        if (ServiceName is not null && string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new ArgumentException("ServiceName must not be empty or whitespace.", nameof(ServiceName));
        }

        if (ResourceAttributes.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Resource attribute keys must not be empty or whitespace.", nameof(ResourceAttributes));
        }
    }
}
