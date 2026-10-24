using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI.Tests;

public class WithDataVolumeAndGpuTests
{
    [Fact]
    public void WithDataVolume_OllamaDefault_MountsVolumeAtOllamaDataPath()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithDataVolume();

        var mount = Assert.Single(ollama.Resource.Annotations.OfType<ContainerMountAnnotation>());

        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("/root/.ollama", mount.Target);
        Assert.Equal("ollama-data", mount.Source);
        Assert.False(mount.IsReadOnly);
    }

    [Fact]
    public void WithDataVolume_OllamaCustomName_UsesProvidedVolumeSource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithDataVolume("models-cache");

        var mount = Assert.Single(ollama.Resource.Annotations.OfType<ContainerMountAnnotation>());

        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("/root/.ollama", mount.Target);
        Assert.Equal("models-cache", mount.Source);
    }

    [Fact]
    public void WithDataVolume_Vllm_MountsVolumeAtHuggingFaceCachePath()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var vllm = appBuilder.AddVllm("vllm", "Qwen/Qwen2.5-7B-Instruct").WithDataVolume();

        var mount = Assert.Single(vllm.Resource.Annotations.OfType<ContainerMountAnnotation>());

        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("/root/.cache/huggingface", mount.Target);
        Assert.Equal("vllm-data", mount.Source);
    }

    [Fact]
    public void WithDataVolume_Called_ReturnsSameBuilderInstance()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama");

        var result = ollama.WithDataVolume();

        Assert.Same(ollama, result);
    }

    [Fact]
    public async Task WithGpu_NvidiaDefault_AddsRuntimeArgsCallbackPassingGpusAll()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu();

        var args = await GetRuntimeArgsAsync(ollama);

        Assert.Equal(["--gpus", "all"], args);
    }

    [Fact]
    public async Task WithGpu_NvidiaCountTwo_PassesGpusTwo()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Nvidia, count: 2);

        var args = await GetRuntimeArgsAsync(ollama);

        Assert.Equal(["--gpus", "2"], args);
    }

    [Fact]
    public async Task WithGpu_Amd_PassesKfdAndDriDevices()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Amd);

        var args = await GetRuntimeArgsAsync(ollama);

        Assert.Equal(["--device", "/dev/kfd", "--device", "/dev/dri"], args);
    }

    [Fact]
    public void WithGpu_AmdOnOllama_SwitchesImageTagToRocm()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Amd);

        var image = Assert.Single(ollama.Resource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("rocm", image.Tag);
    }

    [Fact]
    public void WithGpu_NvidiaDefault_RecordsGpuAnnotationWithVendorAndNullCount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu();

        var annotation = Assert.Single(ollama.Resource.Annotations.OfType<GpuAnnotation>());

        Assert.Equal(GpuVendor.Nvidia, annotation.Vendor);
        Assert.Null(annotation.Count);
    }

    [Fact]
    public void WithGpu_NvidiaCountTwo_RecordsGpuAnnotationWithCount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Nvidia, count: 2);

        var annotation = Assert.Single(ollama.Resource.Annotations.OfType<GpuAnnotation>());

        Assert.Equal(GpuVendor.Nvidia, annotation.Vendor);
        Assert.Equal(2, annotation.Count);
    }

    [Fact]
    public void WithGpu_Amd_RecordsGpuAnnotationWithAmdVendor()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithGpu(GpuVendor.Amd);

        var annotation = Assert.Single(ollama.Resource.Annotations.OfType<GpuAnnotation>());

        Assert.Equal(GpuVendor.Amd, annotation.Vendor);
        Assert.Null(annotation.Count);
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
