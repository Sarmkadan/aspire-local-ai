using Aspire.Hosting;

namespace AspireLocalAI.Tests;

public class VllmResourceTests
{
    private const string ModelName = "Qwen/Qwen2.5-7B-Instruct";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_NullOrWhiteSpaceModelName_ThrowsArgumentException(string? modelName)
    {
        // ThrowIfNullOrWhiteSpace throws ArgumentNullException for null, so accept the base type.
        var ex = Assert.ThrowsAny<ArgumentException>(() => new VllmResource("vllm", modelName!));
        Assert.Equal("modelName", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void AddVllm_PortOutOfRange_ThrowsArgumentOutOfRangeException(int port)
    {
        var builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<ArgumentOutOfRangeException>("port", () => builder.AddVllm("vllm", ModelName, port));
        Assert.Equal(port, ex.ActualValue);
    }

    [Fact]
    public void AddVllm_PortAtUpperBound_DoesNotThrow()
    {
        var builder = DistributedApplication.CreateBuilder();

        var exception = Record.Exception(() => builder.AddVllm("vllm", ModelName, 65535));

        Assert.Null(exception);
    }

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
