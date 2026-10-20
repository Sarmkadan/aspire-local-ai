namespace AspireLocalAI.Tests;

public class OllamaResourceTests
{
    [Fact]
    public void Ctor_WithName_SetsName()
    {
        var resource = new OllamaResource("ollama");

        Assert.Equal("ollama", resource.Name);
    }

    [Fact]
    public void PrimaryEndpoint_EndpointName_EqualsPrimaryEndpointName()
    {
        var resource = new OllamaResource("ollama");

        Assert.Equal(OllamaResource.PrimaryEndpointName, resource.PrimaryEndpoint.EndpointName);
    }

    [Fact]
    public void Models_Initially_IsEmpty()
    {
        var resource = new OllamaResource("ollama");

        Assert.Empty(resource.Models);
    }

    [Fact]
    public void AddModel_WithTag_AppendsToModels()
    {
        var resource = new OllamaResource("ollama");

        resource.AddModel("llama3.2");
        resource.AddModel("nomic-embed-text");

        Assert.Equal(2, resource.Models.Count);
        Assert.Contains("llama3.2", resource.Models);
        Assert.Contains("nomic-embed-text", resource.Models);
    }

    [Fact]
    public void AddModel_DuplicateTag_DoesNotAddTwice()
    {
        var resource = new OllamaResource("ollama");

        resource.AddModel("llama3.2");
        resource.AddModel("llama3.2");

        Assert.Single(resource.Models);
    }

    [Fact]
    public void DefaultPort_Is11434()
    {
        Assert.Equal(11434, OllamaResource.DefaultPort);
    }
}
