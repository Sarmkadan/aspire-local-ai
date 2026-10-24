using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI;

/// <summary>
/// Records a model declared on a local model server via <see cref="LocalAIResourceBuilderExtensions.WithModel{T}"/>,
/// so that hooks (model puller, health check) can read declared models from resource annotations.
/// </summary>
/// <param name="ModelName">Model tag or identifier as passed to <c>WithModel</c>.</param>
internal sealed record ModelAnnotation(string ModelName) : IResourceAnnotation;
