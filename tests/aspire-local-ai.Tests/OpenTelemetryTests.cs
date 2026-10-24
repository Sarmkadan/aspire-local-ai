using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI.Tests;

public class OpenTelemetryTests
{
    [Fact]
    public void WithOpenTelemetry_Ollama_AddsAnnotation()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithOpenTelemetry();

        Assert.Single(ollama.Resource.Annotations.OfType<OpenTelemetryAnnotation>());
    }

    [Fact]
    public async Task WithOpenTelemetry_Ollama_SetsServiceName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("my-ollama").WithOpenTelemetry();

        var env = await GetEnvironmentAsync(ollama);

        Assert.Equal("my-ollama", env["OTEL_SERVICE_NAME"]);
        Assert.False(env.ContainsKey("OLLAMA_DEBUG"));
    }

    [Fact]
    public async Task WithOpenTelemetry_Vllm_EnablesTracesAndPassesEndpointArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var vllm = appBuilder.AddVllm("vllm", "Qwen/Qwen2.5-7B-Instruct").WithOpenTelemetry();

        var env = await GetEnvironmentAsync(vllm);
        var args = await GetArgsAsync(vllm);

        Assert.Equal("1", env["VLLM_OTEL_TRACES_ENABLED"]);
        Assert.Equal("vllm", env["OTEL_SERVICE_NAME"]);
        Assert.Contains("--otlp-traces-endpoint", args);
    }

    [Fact]
    public void WithOpenTelemetry_CalledTwice_AddsOneAnnotation()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var ollama = appBuilder.AddOllama("ollama").WithOpenTelemetry().WithOpenTelemetry();

        Assert.Single(ollama.Resource.Annotations.OfType<OpenTelemetryAnnotation>());
    }

    private static async Task<Dictionary<string, string>> GetEnvironmentAsync<T>(IResourceBuilder<T> builder)
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

        return env.Where(kv => kv.Value is string).ToDictionary(kv => kv.Key, kv => (string)kv.Value);
    }

    private static async Task<List<object>> GetArgsAsync<T>(IResourceBuilder<T> builder)
        where T : IResource
    {
        var args = new List<object>();
        foreach (var annotation in builder.Resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(new CommandLineArgsCallbackContext(args));
        }

        return args;
    }
}
