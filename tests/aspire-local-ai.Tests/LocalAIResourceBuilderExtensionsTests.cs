using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI.Tests;

public class LocalAIResourceBuilderExtensionsTests
{
    [Fact]
    public async Task WithGpu_NvidiaDefault_PassesGpusAll()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu();

        var args = await GetRuntimeArgsAsync(ollama);

        Assert.Equal(["--gpus", "all"], args);
    }

    [Fact]
    public async Task WithGpu_NvidiaWithCount_PassesCount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Nvidia, count: 2);

        var args = await GetRuntimeArgsAsync(ollama);

        Assert.Equal(["--gpus", "2"], args);
    }

    [Fact]
    public async Task WithGpu_Amd_PassesDeviceNodes()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Amd);

        var args = await GetRuntimeArgsAsync(ollama);

        Assert.Equal(["--device", "/dev/kfd", "--device", "/dev/dri"], args);
    }

    [Fact]
    public async Task WithGpu_AmdOnOllama_SwitchesImageTagToRocm()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Amd);

        var image = ollama.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();

        Assert.Equal("rocm", image.Tag);
    }

    [Fact]
    public async Task WithGpu_NvidiaOnOllama_KeepsDefaultImageTag()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Nvidia);

        var image = ollama.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();

        Assert.Equal("latest", image.Tag);
    }

    [Fact]
    public async Task WithGpu_AmdOnVllm_KeepsDefaultImageTag()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var vllm = appBuilder.AddVllm("vllm", "Qwen/Qwen2.5-7B-Instruct").WithGpu(GpuVendor.Amd);

        var image = vllm.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();

        Assert.Equal("latest", image.Tag);
    }

    [Fact]
    public async Task WithGpu_UnknownVendor_ThrowsArgumentOutOfRangeException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama");

        Assert.Throws<ArgumentOutOfRangeException>(() => ollama.WithGpu((GpuVendor)99));
    }

    private static async Task<List<object>> GetRuntimeArgsAsync<T>(IResourceBuilder<T> builder)
        where T : IResource
    {
        var args = new List<object>();
        foreach (var annotation in builder.Resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(new ContainerRuntimeArgsCallbackContext(args, CancellationToken.None));
        }

        return args;
    }
}
