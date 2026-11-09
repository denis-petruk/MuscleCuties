using Microsoft.AspNetCore.Http;
using MuscleCuties.Server;
using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>U-08: the standard header is authoritative; clients released earlier send the legacy name.</summary>
public sealed class IdempotencyKeyResolverTests
{
    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("X-Idempotency-Key")]
    public void HeaderKeyIsAcceptedUnderEitherName(string headerName)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[headerName] = " synthetic-key ";

        Assert.Equal("synthetic-key", IdempotencyKeyResolver.Resolve(context, "body-key"));
    }

    [Fact]
    public void StandardHeaderWinsOverLegacyHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[IdempotencyKeyResolver.LegacyHeaderName] = "legacy";
        context.Request.Headers[IdempotencyKeyResolver.HeaderName] = "standard";

        Assert.Equal("standard", IdempotencyKeyResolver.Resolve(context, null));
    }

    [Fact]
    public void BodyKeyIsTheFallback()
    {
        Assert.Equal("body-key", IdempotencyKeyResolver.Resolve(new DefaultHttpContext(), "body-key"));
        Assert.Equal(string.Empty, IdempotencyKeyResolver.Resolve(new DefaultHttpContext(), null));
    }
}
