using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspireLocalAI.Tests;

public class LocalModelHealthCheckTests
{
    private static readonly Uri Endpoint = new("http://127.0.0.1:11434/");
    private const string OllamaTags = "/api/tags";
    private const string OpenAiModels = "/v1/models";

    [Fact]
    public async Task CheckHealthAsync_OllamaServesAllRequiredModels_ReturnsHealthyWithModels()
    {
        using var client = new HttpClient(new StubHttpMessageHandler()
            .Respond(OllamaTags, HttpStatusCode.OK, """{"models":[{"name":"llama3.2:latest"},{"name":"nomic-embed-text:latest"}]}"""));
        var check = CreateCheck(["llama3.2", "nomic-embed-text"], client, isOllama: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(result.Data.ContainsKey("models"));
        Assert.Equal(new[] { "llama3.2:latest", "nomic-embed-text:latest" }, Assert.IsType<List<string>>(result.Data["models"]));
    }

    [Fact]
    public async Task CheckHealthAsync_OneModelMissingAndRequireAllModels_ReturnsDegradedNamingMissingTag()
    {
        using var client = new HttpClient(new StubHttpMessageHandler()
            .Respond(OllamaTags, HttpStatusCode.OK, """{"models":[{"name":"llama3.2:latest"}]}"""));
        var check = CreateCheck(["llama3.2", "nomic-embed-text"], client, isOllama: true, requireAllModels: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("nomic-embed-text", result.Description);
        Assert.DoesNotContain("llama3.2", result.Description);
        Assert.True(result.Data.ContainsKey("models"));
    }

    [Fact]
    public async Task CheckHealthAsync_OneModelMissingAndRequireAllModelsFalse_ReturnsHealthy()
    {
        using var client = new HttpClient(new StubHttpMessageHandler()
            .Respond(OllamaTags, HttpStatusCode.OK, """{"models":[{"name":"llama3.2:latest"}]}"""));
        var check = CreateCheck(["llama3.2", "nomic-embed-text"], client, isOllama: true, requireAllModels: false);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ProbeThrowsHttpRequestException_ReturnsUnhealthyWithException()
    {
        var failure = new HttpRequestException("connection refused");
        using var client = new HttpClient(new StubHttpMessageHandler().Throw(OllamaTags, failure));
        var check = CreateCheck(["llama3.2"], client, isOllama: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Same(failure, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_EndpointNotAllocated_ReturnsUnhealthy()
    {
        using var client = new HttpClient(new StubHttpMessageHandler());
        var check = new LocalModelHealthCheck(
            _ => Task.FromResult<Uri?>(null),
            ["llama3.2"],
            new LocalAIHealthCheckOptions(),
            client,
            isOllama: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("endpoint not allocated", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_VllmServesAllRequiredModels_ReadsOpenAiDataIdsAndReturnsHealthy()
    {
        using var client = new HttpClient(new StubHttpMessageHandler()
            .Respond(OpenAiModels, HttpStatusCode.OK, """{"object":"list","data":[{"id":"Qwen/Qwen2.5-0.5B-Instruct"}]}"""));
        var check = CreateCheck(["Qwen/Qwen2.5-0.5B-Instruct"], client, isOllama: false);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(new[] { "Qwen/Qwen2.5-0.5B-Instruct" }, Assert.IsType<List<string>>(result.Data["models"]));
    }

    [Fact]
    public async Task CheckHealthAsync_OllamaTagWithLatestOnServer_SatisfiesRequiredNameWithoutTag()
    {
        using var client = new HttpClient(new StubHttpMessageHandler()
            .Respond(OllamaTags, HttpStatusCode.OK, """{"models":[{"name":"foo:latest"}]}"""));
        var check = CreateCheck(["foo"], client, isOllama: true, requireAllModels: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static LocalModelHealthCheck CreateCheck(
        IReadOnlyList<string> requiredModels,
        HttpClient client,
        bool isOllama,
        bool requireAllModels = true) =>
        new(
            _ => Task.FromResult<Uri?>(Endpoint),
            requiredModels,
            new LocalAIHealthCheckOptions { RequireAllModels = requireAllModels },
            client,
            isOllama);
}
