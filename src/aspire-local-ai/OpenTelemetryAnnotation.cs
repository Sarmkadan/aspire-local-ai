using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI;

/// <summary>
/// Marks a local model server for OpenTelemetry export via <see cref="LocalAIResourceBuilderExtensions.WithOpenTelemetry{T}(Aspire.Hosting.ApplicationModel.IResourceBuilder{T})"/>,
/// so that tests and later hooks can detect whether telemetry was requested from resource annotations.
/// </summary>
internal sealed record OpenTelemetryAnnotation : IResourceAnnotation;
