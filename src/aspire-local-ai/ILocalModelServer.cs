using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI;

/// <summary>
/// Common contract for any local LLM server resource (Ollama, vLLM, ...).
/// </summary>
/// <remarks>
/// Allows health checks, telemetry and client integrations to treat different backends uniformly.
/// </remarks>
public interface ILocalModelServer : IResourceWithEndpoints
{
    /// <summary>
    /// Model identifiers this server is expected to serve. For Ollama these are pull tags;
    /// for vLLM this is the single model the process was started with.
    /// </summary>
    IReadOnlyList<string> Models { get; }

    /// <summary>The primary HTTP endpoint clients connect to.</summary>
    EndpointReference PrimaryEndpoint { get; }
}
