namespace AspireLocalAI.Tests;

public class LocalAIHealthCheckOptionsTests
{
    [Fact]
    public void Validate_Defaults_DoesNotThrow()
    {
        var options = new LocalAIHealthCheckOptions();

        options.Validate();

        Assert.Equal(TimeSpan.FromSeconds(10), options.Timeout);
        Assert.True(options.RequireAllModels);
        Assert.Null(options.WarmupPrompt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveTimeout_ThrowsWithParamName(int seconds)
    {
        var options = new LocalAIHealthCheckOptions { Timeout = TimeSpan.FromSeconds(seconds) };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);

        Assert.Equal(nameof(LocalAIHealthCheckOptions.Timeout), ex.ParamName);
    }

    [Fact]
    public void Validate_TimeoutAboveTenMinutes_ThrowsWithParamName()
    {
        var options = new LocalAIHealthCheckOptions { Timeout = TimeSpan.FromMinutes(11) };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);

        Assert.Equal(nameof(LocalAIHealthCheckOptions.Timeout), ex.ParamName);
    }

    [Fact]
    public void Validate_TimeoutExactlyTenMinutes_DoesNotThrow()
    {
        var options = new LocalAIHealthCheckOptions { Timeout = TimeSpan.FromMinutes(10) };

        options.Validate();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validate_WhitespaceWarmupPrompt_ThrowsWithParamName(string prompt)
    {
        var options = new LocalAIHealthCheckOptions { WarmupPrompt = prompt };

        var ex = Assert.Throws<ArgumentException>(options.Validate);

        Assert.Equal(nameof(LocalAIHealthCheckOptions.WarmupPrompt), ex.ParamName);
    }

    [Fact]
    public void Validate_NonWhitespaceWarmupPrompt_DoesNotThrow()
    {
        var options = new LocalAIHealthCheckOptions { WarmupPrompt = "hello" };

        options.Validate();
    }
}
