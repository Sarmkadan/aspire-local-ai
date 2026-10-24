namespace AspireLocalAI.Tests;

public class VllmResourceTests
{
    private const string ModelName = "Qwen/Qwen2.5-7B-Instruct";

    [Fact]
    public void Ctor_WithNameAndModel_SetsNameAndModelName()
    {
        var resource = new VllmResource("vllm", ModelName);

        Assert.Equal("vllm", resource.Name);
        Assert.Equal(ModelName, resource.ModelName);
    }

    [Fact]
    public void Models_Always_ContainsExactlyTheModelName()
    {
        var resource = new VllmResource("vllm", ModelName);

        Assert.Single(resource.Models);
        Assert.Equal(ModelName, resource.Models[0]);
    }

    [Fact]
    public void PrimaryEndpoint_EndpointName_EqualsPrimaryEndpointName()
    {
        var resource = new VllmResource("vllm", ModelName);

        Assert.Equal(VllmResource.PrimaryEndpointName, resource.PrimaryEndpoint.EndpointName);
    }

    [Fact]
    public void DefaultPort_Is8000()
    {
        Assert.Equal(8000, VllmResource.DefaultPort);
    }

    [Fact]
    public void ConnectionStringExpression_ValueExpression_ContainsV1Path()
    {
        var resource = new VllmResource("vllm", ModelName);

        var expression = resource.ConnectionStringExpression.ValueExpression;

        Assert.Contains("/v1", expression);
    }

    [Fact]
    public void ConnectionStringExpression_ValueExpression_ContainsModelName()
    {
        var resource = new VllmResource("vllm", ModelName);

        var expression = resource.ConnectionStringExpression.ValueExpression;

        Assert.Contains($"Model={ModelName}", expression);
    }
}
