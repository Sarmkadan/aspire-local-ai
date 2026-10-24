using System.Net;
using System.Text.Json;

namespace AspireLocalAI.Tests;

public class OllamaModelPullerTests
{
    private static readonly Uri Endpoint = new("http://127.0.0.1:11434/");
    private const string Tags = "/api/tags";
    private const string Pull = "/api/pull";

    [Fact]
    public async Task PullMissingAsync_AllModelsAlreadyInstalled_PostsNothingAndReportsNothing()
    {
        var handler = new StubHttpMessageHandler()
            .Respond(Tags, HttpStatusCode.OK, """{"models":[{"name":"llama3.2:latest"},{"name":"nomic-embed-text:latest"}]}""");
        using var client = new HttpClient(handler);
        var statuses = new List<string>();

        await OllamaModelPuller.PullMissingAsync(Endpoint, ["llama3.2", "nomic-embed-text"], client, statuses.Add, CancellationToken.None);

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Empty(statuses);
    }

    [Fact]
    public async Task PullMissingAsync_OneModelMissing_PostsOnePullWithNameAndNoStreaming()
    {
        var handler = new StubHttpMessageHandler()
            .Respond(Tags, HttpStatusCode.OK, """{"models":[{"name":"llama3.2:latest"}]}""")
            .Respond(Pull, HttpStatusCode.OK, """{"status":"success"}""");
        using var client = new HttpClient(handler);
        var statuses = new List<string>();

        await OllamaModelPuller.PullMissingAsync(Endpoint, ["llama3.2", "nomic-embed-text"], client, statuses.Add, CancellationToken.None);

        var pulls = handler.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        var pull = Assert.Single(pulls);
        Assert.Equal(Pull, pull.Path);
        using var body = JsonDocument.Parse(pull.Body!);
        Assert.Equal("nomic-embed-text", body.RootElement.GetProperty("name").GetString());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(["Pulling nomic-embed-text", "Pulled nomic-embed-text"], statuses);
    }

    [Fact]
    public async Task PullMissingAsync_PullReturnsServerError_ThrowsWithResponseBody()
    {
        const string errorBody = """{"error":"pull model manifest: file does not exist"}""";
        var handler = new StubHttpMessageHandler()
            .Respond(Tags, HttpStatusCode.OK, """{"models":[]}""")
            .Respond(Pull, HttpStatusCode.InternalServerError, errorBody);
        using var client = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OllamaModelPuller.PullMissingAsync(Endpoint, ["missing:1b"], client, _ => { }, CancellationToken.None));

        Assert.Contains(errorBody, ex.Message);
    }

    [Fact]
    public async Task PullMissingAsync_BareNameAndInstalledLatestTag_DoesNotPull()
    {
        var handler = new StubHttpMessageHandler()
            .Respond(Tags, HttpStatusCode.OK, """{"models":[{"name":"foo:latest"}]}""");
        using var client = new HttpClient(handler);
        var statuses = new List<string>();

        await OllamaModelPuller.PullMissingAsync(Endpoint, ["foo"], client, statuses.Add, CancellationToken.None);

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Empty(statuses);
    }

    [Fact]
    public async Task PullMissingAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException()
    {
        var handler = new StubHttpMessageHandler()
            .Respond(Tags, HttpStatusCode.OK, """{"models":[]}""")
            .Respond(Pull, HttpStatusCode.OK, """{"status":"success"}""");
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OllamaModelPuller.PullMissingAsync(Endpoint, ["llama3.2"], client, _ => { }, cts.Token));

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }
}
