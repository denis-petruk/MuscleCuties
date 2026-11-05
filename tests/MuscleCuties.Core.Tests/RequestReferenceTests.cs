using MuscleCuties.Infrastructure.ApiClient;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// G7: only a bounded opaque server reference may be shown. A URL, a token, a path or arbitrary
/// server text is omitted instead of rendered next to a failure.
/// </summary>
public sealed class RequestReferenceTests
{
    [Theory]
    [InlineData("0HN7G2K3Q4M5N:00000001")]
    [InlineData("a1b2c3d4e5f6")]
    [InlineData("trace-9f8e_7d6c.v2")]
    public void BoundedOpaqueReferencesAreDisplayable(string value) =>
        Assert.True(RequestReference.IsDisplayable(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.test/leak?token=abc")]
    [InlineData("/api/v1/workouts/plan/strength-proposals")]
    [InlineData("Bearer abc.def.ghi")]
    [InlineData("user@example.test")]
    [InlineData("../../admin")]
    [InlineData("ref with spaces")]
    [InlineData("ref<script>")]
    public void NonOpaqueOrUnboundedValuesAreOmitted(string? value) =>
        Assert.False(RequestReference.IsDisplayable(value));

    [Fact]
    public void ValuesLongerThanTheBoundAreOmitted()
    {
        Assert.False(RequestReference.IsDisplayable(new string('a', RequestReference.MaximumLength + 1)));
        Assert.True(RequestReference.IsDisplayable(new string('a', RequestReference.MaximumLength)));
    }

    [Fact]
    public void SanitizeTrimsAcceptedValuesAndDropsRejectedOnes()
    {
        Assert.Equal("abc123", RequestReference.Sanitize(" abc123 "));
        Assert.Null(RequestReference.Sanitize("https://example.test/x"));
        Assert.Null(RequestReference.Sanitize(null));
    }
}
