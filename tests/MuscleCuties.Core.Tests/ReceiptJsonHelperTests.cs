using System.Text.Json;
using MuscleCuties.Core.Services.Auth;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class ReceiptJsonHelperTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ExactFieldSetRejectsExtraMissingAndDuplicateFields()
    {
        Assert.True(ReceiptJsonHelper.HasExactFields(Parse("""{"a":1,"b":2}"""), "a", "b"));
        Assert.False(ReceiptJsonHelper.HasExactFields(Parse("""{"a":1,"b":2,"c":3}"""), "a", "b"));
        Assert.False(ReceiptJsonHelper.HasExactFields(Parse("""{"a":1}"""), "a", "b"));
    }

    [Fact]
    public void StringClaimsMustMatchExactly()
    {
        var claims = Parse("""{"alg":"RS256"}""");

        Assert.True(ReceiptJsonHelper.IsString(claims, "alg", "RS256"));
        Assert.False(ReceiptJsonHelper.IsString(claims, "alg", "rs256"));
        Assert.False(ReceiptJsonHelper.IsString(claims, "missing", "RS256"));
        Assert.False(ReceiptJsonHelper.IsString(Parse("""{"alg":1}"""), "alg", "RS256"));
    }

    [Theory]
    [InlineData("""{"kid":"v1"}""", true, "v1")]
    [InlineData("""{"kid":"   "}""", false, "   ")]
    [InlineData("""{"kid":7}""", false, "")]
    [InlineData("""{}""", false, "")]
    public void NonEmptyStringClaimsAreRequired(string json, bool expected, string expectedValue)
    {
        Assert.Equal(expected, ReceiptJsonHelper.TryGetNonemptyString(Parse(json), "kid", out var value));
        Assert.Equal(expectedValue, expected ? value : value ?? string.Empty);
    }

    [Theory]
    [InlineData("""{"iat":17}""", true, 17L)]
    [InlineData("""{"iat":0}""", true, 0L)]
    [InlineData("""{"iat":"17"}""", false, 0L)]
    [InlineData("""{"iat":17.5}""", false, 0L)]
    [InlineData("""{}""", false, 0L)]
    public void Int64ClaimsAreReadOnlyFromNumbers(string json, bool expected, long expectedValue)
    {
        Assert.Equal(expected, ReceiptJsonHelper.TryGetInt64(Parse(json), "iat", out var value));
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void IntClaimsStayInt32()
    {
        Assert.True(ReceiptJsonHelper.TryGetInt(Parse("""{"purpose":3}"""), "purpose", out var value));
        Assert.Equal(3, value);
        Assert.False(ReceiptJsonHelper.TryGetInt(Parse("""{"purpose":3000000000}"""), "purpose", out _));
    }
}
