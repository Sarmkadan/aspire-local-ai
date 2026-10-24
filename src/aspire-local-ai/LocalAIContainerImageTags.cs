namespace AspireLocalAI;

/// <summary>
/// Container image coordinates used by the <c>Add*</c> extension methods.
/// </summary>
internal static class LocalAIContainerImageTags
{
    /// <summary>Ollama image repository.</summary>
    internal const string OllamaImage = "ollama/ollama";

    /// <summary>Registry hosting the Ollama image.</summary>
    internal const string OllamaRegistry = "docker.io";

    /// <summary>Ollama image tag.</summary>
    internal const string OllamaTag = "latest";
}
