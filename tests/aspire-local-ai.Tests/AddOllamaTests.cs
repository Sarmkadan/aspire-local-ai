using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace AspireLocalAI.Tests;

public class AddOllamaTests
{
    [Fact]
    public void AddOllama_WithName_AddsSingleOllamaResourceNamedOllama()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: 12345);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<OllamaResource>());
        Assert.Equal("ollama", resource.Name);
    }

    [Fact]
    public void AddOllama_Resource_HasOllamaContainerImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: 12345);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<OllamaResource>().Single();

        var image = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("ollama/ollama", image.Image);
    }

    [Fact]
    public void AddOllama_WithPort_HttpEndpointTargetsDefaultPortAndBindsRequestedPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: 12345);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<OllamaResource>().Single();

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single(e => e.Name == "http");
        Assert.Equal(11434, endpoint.TargetPort);
        Assert.Equal(12345, endpoint.Port);
    }

    [Fact]
    public void AddOllama_WithoutPort_HttpEndpointPortIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<OllamaResource>().Single();

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single(e => e.Name == "http");
        Assert.Null(endpoint.Port);
    }

    [Fact]
    public void AddOllama_ReturnedBuilder_ResourceIsSameInstanceInModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<OllamaResource> resourceBuilder = builder.AddOllama("ollama", port: 12345);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.OfType<OllamaResource>().Single();

        Assert.Same(resource, resourceBuilder.Resource);
    }
}
