using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace AspireLocalAI.Tests;

public class LocalAIOpenTelemetryOptionsTests
{
    private static readonly IDistributedApplicationBuilder SharedAppBuilder = DistributedApplication.CreateBuilder();

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public void Validate_Defaults_DoesNotThrow()
    {
        var options = new LocalAIOpenTelemetryOptions();

        options.Validate();

        Assert.Null(options.ServiceName);
        Assert.True(options.EnableTraces);
        Assert.True(options.EnableMetrics);
        Assert.Empty(options.ResourceAttributes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validate_WhitespaceServiceName_ThrowsWithParamName(string serviceName)
    {
        var options = new LocalAIOpenTelemetryOptions { ServiceName = serviceName };

        var ex = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(LocalAIOpenTelemetryOptions.ServiceName), ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_EmptyResourceAttributeKey_ThrowsWithParamName(string key)
    {
        var options = new LocalAIOpenTelemetryOptions();
        options.ResourceAttributes[key] = "value";

        var ex = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(LocalAIOpenTelemetryOptions.ResourceAttributes), ex.ParamName);
    }

    [Fact]
    public void WithOpenTelemetry_NullOptions_ThrowsWithParamName()
    {
        var builder = SharedAppBuilder.AddOllama(UniqueName("ollama"));

        var ex = Assert.Throws<ArgumentNullException>(() =>
            LocalAIResourceBuilderExtensions.WithOpenTelemetry(builder, null!));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void WithOpenTelemetry_InvalidOptions_ThrowsAndAddsNoAnnotation()
    {
        var builder = SharedAppBuilder.AddOllama(UniqueName("ollama"));

        Assert.Throws<ArgumentException>(() =>
            builder.WithOpenTelemetry(new LocalAIOpenTelemetryOptions { ServiceName = " " }));

        Assert.Empty(builder.Resource.Annotations.OfType<OpenTelemetryAnnotation>());
    }

    [Fact]
    public async Task WithOpenTelemetry_CustomServiceName_OverridesResourceName()
    {
        var ollama = SharedAppBuilder.AddOllama(UniqueName("ollama"))
            .WithOpenTelemetry(new LocalAIOpenTelemetryOptions { ServiceName = "custom-llm" });

        var env = await GetEnvironmentAsync(ollama);

        Assert.Equal("custom-llm", env["OTEL_SERVICE_NAME"]);
    }

    [Fact]
    public async Task WithOpenTelemetry_ResourceAttributes_JoinedInInsertionOrder()
    {
        var options = new LocalAIOpenTelemetryOptions();
        options.ResourceAttributes["team"] = "ml";
        options.ResourceAttributes["env"] = "dev";
        var ollama = SharedAppBuilder.AddOllama(UniqueName("ollama")).WithOpenTelemetry(options);
        var baseline = SharedAppBuilder.AddOllama(UniqueName("baseline")).WithOtlpExporter();

        var env = await GetEnvironmentAsync(ollama);
        var baselineEnv = await GetEnvironmentAsync(baseline);

        // WithOtlpExporter's service.instance.id entry must survive; our pairs are appended after it.
        Assert.Equal($"{baselineEnv["OTEL_RESOURCE_ATTRIBUTES"]},team=ml,env=dev", env["OTEL_RESOURCE_ATTRIBUTES"]);
    }

    [Fact]
    public async Task WithOpenTelemetry_NoResourceAttributes_LeavesExporterValueUntouched()
    {
        var ollama = SharedAppBuilder.AddOllama(UniqueName("ollama")).WithOpenTelemetry();
        var baseline = SharedAppBuilder.AddOllama(UniqueName("baseline")).WithOtlpExporter();

        var env = await GetEnvironmentAsync(ollama);
        var baselineEnv = await GetEnvironmentAsync(baseline);

        Assert.Equal(baselineEnv["OTEL_RESOURCE_ATTRIBUTES"], env["OTEL_RESOURCE_ATTRIBUTES"]);
    }

    [Fact]
    public async Task WithOpenTelemetry_SignalsEnabledByDefault_DoesNotDisableExporters()
    {
        var ollama = SharedAppBuilder.AddOllama(UniqueName("ollama")).WithOpenTelemetry();

        var env = await GetEnvironmentAsync(ollama);

        Assert.False(env.ContainsKey("OTEL_TRACES_EXPORTER"));
        Assert.False(env.ContainsKey("OTEL_METRICS_EXPORTER"));
    }

    [Fact]
    public async Task WithOpenTelemetry_SignalsDisabled_SetsExportersToNone()
    {
        var options = new LocalAIOpenTelemetryOptions { EnableTraces = false, EnableMetrics = false };
        var ollama = SharedAppBuilder.AddOllama(UniqueName("ollama")).WithOpenTelemetry(options);

        var env = await GetEnvironmentAsync(ollama);

        Assert.Equal("none", env["OTEL_TRACES_EXPORTER"]);
        Assert.Equal("none", env["OTEL_METRICS_EXPORTER"]);
    }

    [Fact]
    public async Task WithOpenTelemetry_VllmTracesDisabled_DoesNotPassTracesEndpoint()
    {
        var options = new LocalAIOpenTelemetryOptions { EnableTraces = false };
        var vllm = SharedAppBuilder.AddVllm(UniqueName("vllm"), "Qwen/Qwen2.5-7B-Instruct").WithOpenTelemetry(options);

        var env = await GetEnvironmentAsync(vllm);
        var args = await GetArgsAsync(vllm);

        Assert.False(env.ContainsKey("VLLM_OTEL_TRACES_ENABLED"));
        Assert.DoesNotContain("--otlp-traces-endpoint", args);
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
