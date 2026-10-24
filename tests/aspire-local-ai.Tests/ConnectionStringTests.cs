using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI.Tests;

public class ConnectionStringTests : IClassFixture<ConnectionStringTests.Fixture>
{
    private readonly Fixture _fixture;

    public ConnectionStringTests(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConnectionStringExpression_Ollama_ResolvesToEndpointWithAllocatedPort()
    {
        AllocateEndpoint(_fixture.Ollama.Resource, 11434);

        var value = await _fixture.Ollama.Resource.ConnectionStringExpression.GetValueAsync(default);

        Assert.Equal("Endpoint=http://localhost:11434", value);
    }

    [Fact]
    public async Task ConnectionStringExpression_Vllm_ResolvesToEndpointWithV1PathAndModel()
    {
        AllocateEndpoint(_fixture.Vllm.Resource, 8000);

        var value = await _fixture.Vllm.Resource.ConnectionStringExpression.GetValueAsync(default);

        Assert.Equal("Endpoint=http://localhost:8000/v1;Model=m", value);
    }

    [Fact]
    public void ConnectionStringExpression_OllamaValueExpression_StartsWithUnresolvedPlaceholder()
    {
        var expression = _fixture.Ollama.Resource.ConnectionStringExpression.ValueExpression;

        Assert.StartsWith("Endpoint=http://{", expression);
    }

    [Fact]
    public async Task WithReference_Ollama_InjectsConnectionStringEnvironmentVariable()
    {
        AllocateEndpoint(_fixture.Ollama.Resource, 11434);

        var env = await GetEnvironmentAsync(_fixture.Api);

        Assert.True(env.TryGetValue("ConnectionStrings__ollama", out var value));
        // The reference is resolved by the orchestrator at run time, so check the resource it points at.
        var reference = Assert.IsType<ConnectionStringReference>(value);
        Assert.Same(_fixture.Ollama.Resource, reference.Resource);
        Assert.Equal(
            "Endpoint=http://localhost:11434",
            await reference.Resource.ConnectionStringExpression.GetValueAsync(default));
    }

    private static void AllocateEndpoint(IResourceWithEndpoints resource, int port)
    {
        var annotation = resource.Annotations.OfType<EndpointAnnotation>().Single();
        annotation.AllocatedEndpoint = new AllocatedEndpoint(annotation, "localhost", port);
    }

    // Values are kept as objects: WithReference injects a ConnectionStringReference that is resolved at run time.
    private static async Task<Dictionary<string, object>> GetEnvironmentAsync<T>(IResourceBuilder<T> builder)
        where T : IResource
    {
        var env = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)),
            env);
        foreach (var annotation in builder.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return env;
    }

    // One application model for the whole class: every DistributedApplication.CreateBuilder() call leaks
    // file watchers, and the test host runs out of inotify instances if each test creates its own.
    public sealed class Fixture
    {
        public Fixture()
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            Ollama = appBuilder.AddOllama("ollama");
            Vllm = appBuilder.AddVllm("vllm", "m");
            Api = appBuilder.AddContainer("api", "img").WithReference(Ollama);
        }

        public IResourceBuilder<OllamaResource> Ollama { get; }

        public IResourceBuilder<VllmResource> Vllm { get; }

        public IResourceBuilder<ContainerResource> Api { get; }
    }
}
