using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI;

/// <summary>
/// Records a GPU passthrough request made via <see cref="LocalAIResourceBuilderExtensions.WithGpu{T}"/>,
/// so that later hooks can read the requested vendor and device count from resource annotations.
/// </summary>
/// <param name="Vendor">GPU vendor the container was asked to use.</param>
/// <param name="Count">Number of devices, or <see langword="null"/> for all.</param>
internal sealed record GpuAnnotation(GpuVendor Vendor, int? Count) : IResourceAnnotation;
