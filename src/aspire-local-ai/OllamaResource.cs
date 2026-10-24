using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI;

/// <summary>
/// Represents an Ollama server running as a container resource inside an Aspire application model.
/// </summary>
/// <remarks>
/// The resource exposes a single HTTP endpoint (default port 11434) and publishes a connection string
/// of the form <c>Endpoint=http://host:port</c> so that consuming projects can bind an Ollama client to it.
/// Models declared via <see cref="LocalAIResourceBuilderExtensions.WithModel{T}"/> are pulled on startup.
/// </remarks>
public sealed class OllamaResource : ContainerResource, IResourceWithConnectionString, ILocalModelServer
{
    /// <summary>Name of the primary HTTP endpoint.</summary>
    public const string PrimaryEndpointName = "http";

    /// <summary>Default port Ollama listens on inside the container.</summary>
    public const int DefaultPort = 11434;

    /// <summary>Path inside the container where Ollama stores downloaded model weights.</summary>
    internal const string DataMountPath = "/root/.ollama";

    private readonly List<string> _models = [];
    private readonly EndpointReference _primaryEndpoint;

    /// <summary>
    /// Initializes a new <see cref="OllamaResource"/>.
    /// </summary>
    /// <param name="name">Resource name, unique within the application model.</param>
    public OllamaResource(string name) : base(name)
    {
        _primaryEndpoint = new EndpointReference(this, PrimaryEndpointName);
    }

    /// <summary>The primary HTTP endpoint reference for this resource.</summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint;

    /// <inheritdoc />
    public IReadOnlyList<string> Models => _models.AsReadOnly();

    /// <inheritdoc />
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Endpoint=http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    /// <summary>
    /// Adds a model tag (for example <c>llama3.2</c> or <c>nomic-embed-text</c>) that must be available
    /// on the server before it is considered healthy.
    /// </summary>
    /// <param name="modelName">Ollama model tag.</param>
    public void AddModel(string modelName)
    {
        if (!_models.Contains(modelName))
        {
            _models.Add(modelName);
        }
    }
}
