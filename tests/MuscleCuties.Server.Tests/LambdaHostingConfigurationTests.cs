using Microsoft.Extensions.Configuration;
using MuscleCuties.Server;
using Npgsql;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class LambdaHostingConfigurationTests
{
    private const string BaseConnectionString =
        "Host=staging-db.local;Port=5432;Database=musclecuties_staging;Username=mc_app;Password=secret";

    [Fact]
    public void ResolveOptions_WithDefaultConfiguration_ReturnsDefaultBoundedValues()
    {
        var config = new ConfigurationBuilder().Build();

        var options = LambdaHostingConfiguration.ResolveOptions(config);

        Assert.Equal(LambdaHostingConfiguration.DefaultLambdaMinPoolSize, options.MinPoolSize);
        Assert.Equal(LambdaHostingConfiguration.DefaultLambdaMaxPoolSize, options.MaxPoolSize);
        Assert.Equal(LambdaHostingConfiguration.DefaultConnectionIdleLifetimeSeconds, options.ConnectionIdleLifetimeSeconds);
        Assert.Equal(LambdaHostingConfiguration.DefaultConnectionTimeoutSeconds, options.ConnectionTimeoutSeconds);
        Assert.Equal(LambdaHostingConfiguration.DefaultCommandTimeoutSeconds, options.CommandTimeoutSeconds);
        Assert.True(options.RequireSslMode);
        Assert.False(options.RunMigrationsOnColdStart);
    }

    [Fact]
    public void ResolveOptions_WithCustomValidConfiguration_ReturnsConfiguredValues()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Lambda:DatabasePool:MinPoolSize"] = "1",
            ["Lambda:DatabasePool:MaxPoolSize"] = "10",
            ["Lambda:DatabasePool:ConnectionIdleLifetimeSeconds"] = "120",
            ["Lambda:DatabasePool:ConnectionTimeoutSeconds"] = "15",
            ["Lambda:DatabasePool:CommandTimeoutSeconds"] = "30",
            ["Lambda:DatabasePool:RequireSslMode"] = "true",
            ["Lambda:DatabasePool:RunMigrationsOnColdStart"] = "false"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var options = LambdaHostingConfiguration.ResolveOptions(config);

        Assert.Equal(1, options.MinPoolSize);
        Assert.Equal(10, options.MaxPoolSize);
        Assert.Equal(120, options.ConnectionIdleLifetimeSeconds);
        Assert.Equal(15, options.ConnectionTimeoutSeconds);
        Assert.Equal(30, options.CommandTimeoutSeconds);
        Assert.True(options.RequireSslMode);
        Assert.False(options.RunMigrationsOnColdStart);
    }

    [Fact]
    public void ResolveOptions_WhenColdStartMigrationRequested_ThrowsInvalidOperationException()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Lambda:DatabasePool:RunMigrationsOnColdStart"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => LambdaHostingConfiguration.ResolveOptions(config));
        Assert.Contains("must never execute on AWS Lambda cold start", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0", "0")]    // MaxPoolSize <= 0
    [InlineData("-1", "-1")]  // Negative
    [InlineData("5", "26")]   // MaxPoolSize > 25
    [InlineData("-1", "5")]   // MinPoolSize < 0
    [InlineData("10", "5")]   // MinPoolSize > MaxPoolSize
    public void ResolveOptions_WithInvalidPoolBounds_ThrowsInvalidOperationException(string min, string max)
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Lambda:DatabasePool:MinPoolSize"] = min,
            ["Lambda:DatabasePool:MaxPoolSize"] = max
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => LambdaHostingConfiguration.ResolveOptions(config));
        Assert.Contains("must be bounded between 0 and 25", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveOptions_WhenRequireSslModeIsFalse_ThrowsInvalidOperationException()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Lambda:DatabasePool:RequireSslMode"] = "false"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => LambdaHostingConfiguration.ResolveOptions(config));
        Assert.Contains("require full TLS certificate verification", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0", "10", "20")]   // IdleLifetime <= 0
    [InlineData("-5", "10", "20")]
    [InlineData("60", "0", "20")]   // ConnectionTimeout <= 0
    [InlineData("60", "-1", "20")]
    [InlineData("60", "10", "0")]   // CommandTimeout <= 0
    [InlineData("60", "10", "-2")]
    public void ResolveOptions_WithNonPositiveTimeouts_ThrowsInvalidOperationException(
        string idleLifetime,
        string connTimeout,
        string cmdTimeout)
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Lambda:DatabasePool:ConnectionIdleLifetimeSeconds"] = idleLifetime,
            ["Lambda:DatabasePool:ConnectionTimeoutSeconds"] = connTimeout,
            ["Lambda:DatabasePool:CommandTimeoutSeconds"] = cmdTimeout
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => LambdaHostingConfiguration.ResolveOptions(config));
        Assert.Contains("must be positive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveOptions_WithNullConfiguration_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LambdaHostingConfiguration.ResolveOptions(null!));
    }

    [Fact]
    public void BuildBoundedRdsProxyConnectionString_EnforcesTlsAndPoolBounds()
    {
        var options = new LambdaRdsProxyPoolOptions(
            MinPoolSize: 1,
            MaxPoolSize: 8,
            ConnectionIdleLifetimeSeconds: 45,
            ConnectionTimeoutSeconds: 8,
            CommandTimeoutSeconds: 15,
            RequireSslMode: true,
            RunMigrationsOnColdStart: false);

        var result = LambdaHostingConfiguration.BuildBoundedRdsProxyConnectionString(BaseConnectionString, options);

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.True(builder.Pooling);
        Assert.Equal(1, builder.MinPoolSize);
        Assert.Equal(8, builder.MaxPoolSize);
        Assert.Equal(45, builder.ConnectionIdleLifetime);
        Assert.Equal(8, builder.Timeout);
        Assert.Equal(15, builder.CommandTimeout);
        Assert.Equal(SslMode.VerifyFull, builder.SslMode);
        Assert.False(builder.NoResetOnClose);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildBoundedRdsProxyConnectionString_WithNullOrEmptyBase_ThrowsArgumentException(string? invalid)
    {
        var options = new LambdaRdsProxyPoolOptions(0, 5, 60, 10, 20, true, false);

        Assert.ThrowsAny<ArgumentException>(() =>
            LambdaHostingConfiguration.BuildBoundedRdsProxyConnectionString(invalid!, options));
    }

    [Fact]
    public void BuildBoundedRdsProxyConnectionString_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LambdaHostingConfiguration.BuildBoundedRdsProxyConnectionString(BaseConnectionString, null!));
    }

    [Fact]
    public void IsRunningInLambda_ReflectsEnvironmentVariable()
    {
        var original = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
        try
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
            Assert.False(LambdaHostingConfiguration.IsRunningInLambda());

            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "musclecuties-backend-api");
            Assert.True(LambdaHostingConfiguration.IsRunningInLambda());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", original);
        }
    }
}
