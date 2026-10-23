using System.Collections.Concurrent;
using System.Diagnostics;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class Pg09LeastPrivilegeAndLoadTests(ITestOutputHelper output)
{
    private const string ConnectionVariable = "MC_PG09_LOAD_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string DisposableDatabaseName = "mc_pg09_disposable";
    private const string LoadApplicationName = "mc_pg09_load";
    private const string ObserverApplicationName = "mc_pg09_observer";
    private const int MaximumPoolConnections = 4;
    private const int MinimumPoolConnections = 0;
    private const int WorkerCount = 8;
    private const int DurationMinutes = 3;
    private const int SampleIntervalSeconds = 5;
    private const int ConnectionHoldMilliseconds = 25;
    private const int MinimumSamples = 30;
    private const int MinimumRequests = 100;
    private const int QueryTimeoutSeconds = 10;
    private const int ConnectionTimeoutSeconds = 10;
    private const double NinetyFifthPercentile = 0.95;

    [Fact]
    public async Task DisposableFixtureUsesRestrictedMigratorAndBoundedPool()
    {
        var baseConnection = RequireDisposableConnection();
        var loadBuilder = new NpgsqlConnectionStringBuilder(baseConnection)
        {
            ApplicationName = LoadApplicationName,
            MaxPoolSize = MaximumPoolConnections,
            MinPoolSize = MinimumPoolConnections,
            Timeout = ConnectionTimeoutSeconds,
            CommandTimeout = QueryTimeoutSeconds,
            Pooling = true
        };
        var observerBuilder = new NpgsqlConnectionStringBuilder(baseConnection)
        {
            ApplicationName = ObserverApplicationName,
            Pooling = false,
            Timeout = ConnectionTimeoutSeconds,
            CommandTimeout = QueryTimeoutSeconds
        };

        await using var observer = new NpgsqlConnection(observerBuilder.ConnectionString);
        await observer.OpenAsync();
        await using (var roleCommand = new NpgsqlCommand(
            "SELECT rolsuper, rolbypassrls, rolcreaterole FROM pg_roles WHERE rolname = current_user", observer))
        await using (var reader = await roleCommand.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.GetBoolean(reader.GetOrdinal("rolsuper")));
            Assert.False(reader.GetBoolean(reader.GetOrdinal("rolbypassrls")));
            Assert.True(reader.GetBoolean(reader.GetOrdinal("rolcreaterole")));
        }

        var duration = TimeSpan.FromMinutes(DurationMinutes);
        var samples = new ConcurrentQueue<(long ManagedBytes, long WorkingSetBytes, long BackendCount)>();
        var simulatedCycleLatencies = new ConcurrentQueue<double>();
        var queryLatencies = new ConcurrentQueue<double>();
        var acquisitionLatencies = new ConcurrentQueue<double>();
        var errors = new ConcurrentQueue<string>();
        using var stop = new CancellationTokenSource(duration);
        var stopwatch = Stopwatch.StartNew();

        async Task WorkerAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    await using var connection = new NpgsqlConnection(loadBuilder.ConnectionString);
                    await connection.OpenAsync(stop.Token);
                    acquisitionLatencies.Enqueue(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    var queryStarted = Stopwatch.GetTimestamp();
                    await using var command = new NpgsqlCommand(
                        "SELECT count(*) FROM catalog.quiz_questions", connection);
                    var count = (long)(await command.ExecuteScalarAsync(stop.Token) ?? 0L);
                    queryLatencies.Enqueue(Stopwatch.GetElapsedTime(queryStarted).TotalMilliseconds);
                    Assert.True(count > 0, "Canonical quiz seed must be present.");
                    await Task.Delay(ConnectionHoldMilliseconds, stop.Token);
                    simulatedCycleLatencies.Enqueue(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    errors.Enqueue(exception.GetType().Name);
                    stop.Cancel();
                    break;
                }
            }
        }

        async Task SampleAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                await using var command = new NpgsqlCommand(
                    "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND application_name = @name", observer);
                command.Parameters.AddWithValue("name", LoadApplicationName);
                var backendCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
                using var process = Process.GetCurrentProcess();
                samples.Enqueue((GC.GetTotalMemory(false), process.WorkingSet64, backendCount));
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(SampleIntervalSeconds), stop.Token);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        var workers = Enumerable.Range(0, WorkerCount).Select(_ => WorkerAsync()).ToArray();
        await Task.WhenAll(workers.Append(SampleAsync()));
        stopwatch.Stop();
        var readings = samples.ToArray();
        var timings = simulatedCycleLatencies.Order().ToArray();
        var queryTimings = queryLatencies.Order().ToArray();
        var acquisitionTimings = acquisitionLatencies.Order().ToArray();
        var p95Index = timings.Length == 0 ? 0 : (int)Math.Ceiling(timings.Length * NinetyFifthPercentile) - 1;
        var queryP95Index = queryTimings.Length == 0 ? 0 : (int)Math.Ceiling(queryTimings.Length * NinetyFifthPercentile) - 1;
        var acquisitionP95Index = acquisitionTimings.Length == 0 ? 0 :
            (int)Math.Ceiling(acquisitionTimings.Length * NinetyFifthPercentile) - 1;
        output.WriteLine($"elapsed_s={stopwatch.Elapsed.TotalSeconds:F1} requests={timings.Length} errors={errors.Count} " +
                         $"query_p95_ms={(queryTimings.Length == 0 ? 0 : queryTimings[queryP95Index]):F1} " +
                         $"simulated_cycle_p95_ms={(timings.Length == 0 ? 0 : timings[p95Index]):F1} " +
                         $"acquire_p95_ms={(acquisitionTimings.Length == 0 ? 0 : acquisitionTimings[acquisitionP95Index]):F1} " +
                         $"samples={readings.Length} max_backends={(readings.Length == 0 ? 0 : readings.Max(x => x.BackendCount))} " +
                         $"managed_first_last_bytes={(readings.Length == 0 ? 0 : readings[0].ManagedBytes)}/" +
                         $"{(readings.Length == 0 ? 0 : readings[^1].ManagedBytes)} " +
                         $"working_set_first_last_bytes={(readings.Length == 0 ? 0 : readings[0].WorkingSetBytes)}/" +
                         $"{(readings.Length == 0 ? 0 : readings[^1].WorkingSetBytes)}");
        for (var index = 0; index < readings.Length; index++)
            output.WriteLine($"sample={index} managed_bytes={readings[index].ManagedBytes} " +
                             $"working_set_bytes={readings[index].WorkingSetBytes} " +
                             $"backend_connections={readings[index].BackendCount}");
        Assert.Empty(errors);
        Assert.True(timings.Length >= MinimumRequests);
        Assert.True(readings.Length >= MinimumSamples);
        Assert.All(readings, sample => Assert.InRange(sample.BackendCount, 0L, (long)MaximumPoolConnections));
    }

    private static string RequireDisposableConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a seeded disposable PostgreSQL fixture.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal(LocalHost, builder.Host);
        Assert.Equal(DisposableDatabaseName, builder.Database);
        return connectionString!;
    }
}
