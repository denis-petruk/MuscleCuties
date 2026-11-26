using MuscleCuties.UI.Services.Auth;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class OtpCodeRulesTests
{
    [Theory]
    [InlineData("123456", "123456")]
    [InlineData("12 34-56", "123456")]
    [InlineData("1234567", "123456")]
    [InlineData("abc", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void DigitsOnlyKeepsSixDigits(string? input, string expected)
    {
        Assert.Equal(expected, OtpCodeRules.DigitsOnly(input));
    }

    [Theory]
    [InlineData("123456", null)]
    [InlineData("12 34-56", "123456")]
    [InlineData("1234567", "123456")]
    [InlineData("abc", "")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void RewriteToDigitsReturnsOnlyWhenInputChanges(string? input, string? expected)
    {
        Assert.Equal(expected, OtpCodeRules.RewriteToDigits(input));
    }
}
