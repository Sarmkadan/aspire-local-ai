using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace AspireLocalAI.Tests;

public class AddVllmTests
{
    private const string ModelName = "Qwen/Qwen2.5-7B-Instruct";

    [Fact]
    public void AddVllm_WithNameAndModel_AddsSingleVllmResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVllm("vllm", ModelName, 9000);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<VllmResource>());
        Assert.Equal("vllm", resource.Name);
    }

    [Fact]
    public void AddVllm_Resource_ModelNameMatchesRequestedModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVllm("vllm", ModelName, 9000);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<VllmResource>().Single();

        Assert.Equal(ModelName, resource.ModelName);
    }

    [Fact]
    public void AddVllm_Resource_HasVllmContainerImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVllm("vllm", ModelName, 9000);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<VllmResource>().Single();

        var image = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("vllm/vllm-openai", image.Image);
    }

    [Fact]
    public void AddVllm_WithPort_HttpEndpointTargetsDefaultPortAndBindsRequestedPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVllm("vllm", ModelName, 9000);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<VllmResource>().Single();

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single(e => e.Name == "http");
        Assert.Equal(8000, endpoint.TargetPort);
        Assert.Equal(9000, endpoint.Port);
    }

    [Fact]
    public async Task AddVllm_Resource_CommandLineArgsIncludeModelFlagWithModelId()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVllm("vllm", ModelName, 9000);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<VllmResource>().Single();

        var annotation = Assert.Single(resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>());

        var args = new List<object>();
        var context = new CommandLineArgsCallbackContext(args, resource, CancellationToken.None);
        await annotation.Callback(context);

        var values = args.Select(a => a.ToString()).ToList();
        var index = values.IndexOf("--model");
        Assert.True(index >= 0, "Expected '--model' in vLLM arguments.");
        Assert.Equal(ModelName, values[index + 1]);
    }
}
