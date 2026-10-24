using Aspire.Hosting;

namespace AspireLocalAI.Tests;

public class WithModelTests
{
    [Fact]
    public void WithModel_TwoModels_ResourceModelsInDeclarationOrder()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ollama = builder.AddOllama("ollama")
            .WithModel("llama3.2")
            .WithModel("nomic-embed-text");

        Assert.Equal(new[] { "llama3.2", "nomic-embed-text" }, ollama.Resource.Models);
    }

    [Fact]
    public void WithModel_TwoModels_AddsOneModelAnnotationPerCallInOrder()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ollama = builder.AddOllama("ollama")
            .WithModel("llama3.2")
            .WithModel("nomic-embed-text");

        var annotations = ollama.Resource.Annotations.OfType<ModelAnnotation>().ToList();

        Assert.Equal(2, annotations.Count);
        Assert.Equal(new[] { "llama3.2", "nomic-embed-text" }, annotations.Select(a => a.ModelName));
    }

    [Fact]
    public void WithModel_OnVllm_ThrowsInvalidOperationExceptionWithResourceName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVllm("vllm", "Qwen/Qwen2.5-7B-Instruct");

        var ex = Assert.Throws<InvalidOperationException>(() => vllm.WithModel("llama3.2"));

        Assert.Contains("vllm", ex.Message);
    }

    [Fact]
    public void WithModel_Called_ReturnsSameBuilderInstance()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ollama = builder.AddOllama("ollama");

        var result = ollama.WithModel("llama3.2");

        Assert.Same(ollama, result);
    }
}
