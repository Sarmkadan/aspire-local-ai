using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI;

/// <summary>
/// Represents a vLLM inference server running as a container resource inside an Aspire application model.
/// </summary>
/// <remarks>
/// vLLM serves exactly one model per process and exposes an OpenAI-compatible HTTP API (default port 8000).
/// The published connection string has the form <c>Endpoint=http://host:port/v1;Model=name</c>.
/// GPU passthrough is required for realistic performance; see <see cref="LocalAIResourceBuilderExtensions.WithGpu{T}"/>.
/// </remarks>
public sealed class VllmResource : ContainerResource, IResourceWithConnectionString, ILocalModelServer
{
    /// <summary>Name of the primary HTTP endpoint.</summary>
    public const string PrimaryEndpointName = "http";

    /// <summary>Default port vLLM listens on inside the container.</summary>
    public const int DefaultPort = 8000;

    /// <summary>
    /// Initializes a new <see cref="VllmResource"/>.
    /// </summary>
    /// <param name="name">Resource name, unique within the application model.</param>
    /// <param name="modelName">Hugging Face model id served by this instance, e.g. <c>Qwen/Qwen2.5-7B-Instruct</c>.</param>
    public VllmResource(string name, string modelName) : base(name)
    {
        throw new NotImplementedException();
    }

    /// <summary>The Hugging Face model id served by this instance.</summary>
    public string ModelName => throw new NotImplementedException();

    /// <summary>The primary HTTP endpoint reference for this resource.</summary>
    public EndpointReference PrimaryEndpoint => throw new NotImplementedException();

    /// <inheritdoc />
    public IReadOnlyList<string> Models => throw new NotImplementedException();

    /// <inheritdoc />
    public ReferenceExpression ConnectionStringExpression => throw new NotImplementedException();
}
