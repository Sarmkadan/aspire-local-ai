using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI.Tests;

public class LocalAIResourceBuilderExtensionsGuardTests
{
    // One builder per class: each CreateBuilder() opens file watchers, and the host's inotify limit is easily exhausted.
    // Every invalid-argument path throws before the resource is added, so sharing is safe.
    private static readonly IDistributedApplicationBuilder SharedAppBuilder = DistributedApplication.CreateBuilder();

    [Theory]
    [InlineData(true, "ollama", null, typeof(ArgumentNullException), "builder")]
    [InlineData(false, null, null, typeof(ArgumentNullException), "name")]
    [InlineData(false, "", null, typeof(ArgumentException), "name")]
    [InlineData(false, "   ", null, typeof(ArgumentException), "name")]
    [InlineData(false, "ollama", 0, typeof(ArgumentOutOfRangeException), "port")]
    [InlineData(false, "ollama", -1, typeof(ArgumentOutOfRangeException), "port")]
    [InlineData(false, "ollama", 65536, typeof(ArgumentOutOfRangeException), "port")]
    public void AddOllama_InvalidArgument_ThrowsWithParamName(
        bool nullBuilder, string? name, int? port, Type expected, string paramName)
    {
        var appBuilder = nullBuilder ? null : SharedAppBuilder;

        AssertArgumentException(expected, paramName, () =>
            LocalAIResourceBuilderExtensions.AddOllama(appBuilder!, name!, port));
    }

    [Theory]
    [InlineData(true, "vllm", "Qwen/Qwen2.5-7B-Instruct", null, typeof(ArgumentNullException), "builder")]
    [InlineData(false, null, "Qwen/Qwen2.5-7B-Instruct", null, typeof(ArgumentNullException), "name")]
    [InlineData(false, " ", "Qwen/Qwen2.5-7B-Instruct", null, typeof(ArgumentException), "name")]
    [InlineData(false, "vllm", null, null, typeof(ArgumentNullException), "modelName")]
    [InlineData(false, "vllm", "  ", null, typeof(ArgumentException), "modelName")]
    [InlineData(false, "vllm", "Qwen/Qwen2.5-7B-Instruct", 0, typeof(ArgumentOutOfRangeException), "port")]
    [InlineData(false, "vllm", "Qwen/Qwen2.5-7B-Instruct", 65536, typeof(ArgumentOutOfRangeException), "port")]
    public void AddVllm_InvalidArgument_ThrowsWithParamName(
        bool nullBuilder, string? name, string? modelName, int? port, Type expected, string paramName)
    {
        var appBuilder = nullBuilder ? null : SharedAppBuilder;

        AssertArgumentException(expected, paramName, () =>
            LocalAIResourceBuilderExtensions.AddVllm(appBuilder!, name!, modelName!, port));
    }

    [Theory]
    [InlineData(true, "qwen3:8b", typeof(ArgumentNullException), "builder")]
    [InlineData(false, null, typeof(ArgumentNullException), "modelName")]
    [InlineData(false, "", typeof(ArgumentException), "modelName")]
    public void WithModel_InvalidArgument_ThrowsWithParamName(
        bool nullBuilder, string? modelName, Type expected, string paramName)
    {
        var builder = OllamaBuilder(nullBuilder);

        AssertArgumentException(expected, paramName, () =>
            LocalAIResourceBuilderExtensions.WithModel(builder!, modelName!));
    }

    [Theory]
    [InlineData(true, "models", typeof(ArgumentNullException), "builder")]
    [InlineData(false, "", typeof(ArgumentException), "name")]
    [InlineData(false, "   ", typeof(ArgumentException), "name")]
    public void WithDataVolume_InvalidArgument_ThrowsWithParamName(
        bool nullBuilder, string? name, Type expected, string paramName)
    {
        var builder = OllamaBuilder(nullBuilder);

        AssertArgumentException(expected, paramName, () =>
            LocalAIResourceBuilderExtensions.WithDataVolume(builder!, name));
    }

    [Theory]
    [InlineData(true, GpuVendor.Nvidia, null, typeof(ArgumentNullException), "builder")]
    [InlineData(false, (GpuVendor)99, null, typeof(ArgumentOutOfRangeException), "vendor")]
    [InlineData(false, GpuVendor.Nvidia, 0, typeof(ArgumentOutOfRangeException), "count")]
    [InlineData(false, GpuVendor.Amd, -1, typeof(ArgumentOutOfRangeException), "count")]
    public void WithGpu_InvalidArgument_ThrowsWithParamName(
        bool nullBuilder, GpuVendor vendor, int? count, Type expected, string paramName)
    {
        var builder = OllamaBuilder(nullBuilder);

        AssertArgumentException(expected, paramName, () =>
            LocalAIResourceBuilderExtensions.WithGpu(builder!, vendor, count));
    }

    [Theory]
    [InlineData(true, typeof(ArgumentNullException), "builder")]
    public void WithModelHealthCheck_InvalidArgument_ThrowsWithParamName(
        bool nullBuilder, Type expected, string paramName)
    {
        var builder = OllamaBuilder(nullBuilder);

        AssertArgumentException(expected, paramName, () =>
            LocalAIResourceBuilderExtensions.WithModelHealthCheck(builder!));
    }

    [Theory]
    [InlineData(true, typeof(ArgumentNullException), "builder")]
    public void WithOpenTelemetry_InvalidArgument_ThrowsWithParamName(
        bool nullBuilder, Type expected, string paramName)
    {
        var builder = OllamaBuilder(nullBuilder);

        AssertArgumentException(expected, paramName, () =>
            LocalAIResourceBuilderExtensions.WithOpenTelemetry(builder!));
    }

    private static IResourceBuilder<OllamaResource>? OllamaBuilder(bool nullBuilder) =>
        nullBuilder ? null : SharedAppBuilder.AddOllama($"ollama-{Guid.NewGuid():N}");

    private static void AssertArgumentException(Type expected, string paramName, Action action)
    {
        var ex = Assert.Throws(expected, action);
        Assert.Equal(paramName, ((ArgumentException)ex).ParamName);
    }
}
