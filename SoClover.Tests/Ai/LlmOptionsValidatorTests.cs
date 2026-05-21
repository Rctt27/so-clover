using Microsoft.Extensions.Options;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.Ai;

public class LlmOptionsValidatorTests
{
    [Fact]
    public void Default_options_are_valid()
    {
        var validator = new LlmOptionsValidator();
        var opts = new LlmOptions();

        var result = validator.Validate(name: null, options: opts);

        Assert.True(result.Succeeded, $"Default options should be valid. Failures: {string.Join("; ", result.Failures ?? Array.Empty<string>())}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(17)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void MaxConcurrency_outside_1_to_16_is_rejected(int invalid)
    {
        var validator = new LlmOptionsValidator();
        var opts = new LlmOptions { MaxConcurrency = invalid };

        var result = validator.Validate(name: null, options: opts);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("MaxConcurrency", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    public void MaxConcurrency_in_1_to_16_is_accepted(int valid)
    {
        var validator = new LlmOptionsValidator();
        var opts = new LlmOptions { MaxConcurrency = valid };

        var result = validator.Validate(name: null, options: opts);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxCallsPerGame_must_be_positive(int invalid)
    {
        var validator = new LlmOptionsValidator();
        var opts = new LlmOptions { MaxCallsPerGame = invalid };

        var result = validator.Validate(name: null, options: opts);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("MaxCallsPerGame", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void TimeoutSeconds_must_be_positive(int invalid)
    {
        var validator = new LlmOptionsValidator();
        var opts = new LlmOptions { TimeoutSeconds = invalid };

        var result = validator.Validate(name: null, options: opts);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("TimeoutSeconds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Empty_BaseUrl_is_rejected()
    {
        var validator = new LlmOptionsValidator();
        var opts = new LlmOptions { BaseUrl = "" };

        var result = validator.Validate(name: null, options: opts);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Empty_DefaultModel_is_rejected()
    {
        var validator = new LlmOptionsValidator();
        var opts = new LlmOptions { DefaultModel = "" };

        var result = validator.Validate(name: null, options: opts);

        Assert.True(result.Failed);
    }
}
