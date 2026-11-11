using System.Diagnostics;
using System.Reflection;
using MuscleCuties.DbMaintenance;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

[Collection(DisposableDatabaseCollection.Name)]
public sealed class SessionLockRunnerTests(DisposableDatabaseFixture fixture)
{
    private const int ChildSleepSeconds = 30;
    private const int LockWaitSeconds = 20;
    private const int RunnerExitWaitSeconds = 10;
    private const int LockPollMilliseconds = 100;

    [PostgresFact]
    public async Task TerminatesChildWhenAdvisoryLockSessionIsLost()
    {
        if (!fixture.Available || OperatingSystem.IsWindows()) return;

        var connectionString = await fixture.CreateCopyAsync("runner_lock_loss");
        try
        {
            var root = FindRepositoryRoot();
            var configuration = typeof(SessionLockRunnerTests).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            var maintenanceDll = Path.Combine(root, "src", "MuscleCuties.DbMaintenance", "bin", configuration,
                "net10.0", "MuscleCuties.DbMaintenance.dll");
            Assert.True(File.Exists(maintenanceDll), "Build the maintenance project before running this test.");
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in new[]
                     {
                         maintenanceDll, "session-lock-runner", "--", "/bin/sh", "-c", $"sleep {ChildSleepSeconds}"
                     })
                start.ArgumentList.Add(argument);
            start.Environment["ConnectionStrings__PostgreSql"] = connectionString;
            start.Environment.Remove("MC_RELEASE_OFFLINE_TEST");
            start.Environment.Remove("AWS_LAMBDA_FUNCTION_NAME");

            using var runner = Process.Start(start) ?? throw new InvalidOperationException("Lock runner did not start.");
            try
            {
                var backendPid = await WaitForLockBackendAsync(connectionString);
                Assert.True(backendPid > 0, "Lock runner never acquired the advisory lock.");

                await using var terminator = new NpgsqlConnection(connectionString);
                await terminator.OpenAsync();
                await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", terminator);
                terminate.Parameters.AddWithValue("pid", backendPid);
                Assert.True(await terminate.ExecuteScalarAsync() is true);

                await runner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(RunnerExitWaitSeconds));
                Assert.NotEqual(0, runner.ExitCode);
            }
            finally
            {
                if (!runner.HasExited)
                    runner.Kill(entireProcessTree: true);
                await runner.WaitForExitAsync();
            }
        }
        finally
        {
            await fixture.DropAsync(connectionString);
        }
    }

    private static async Task<int> WaitForLockBackendAsync(string connectionString)
    {
        var deadline = DateTime.UtcNow.AddSeconds(LockWaitSeconds);
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync();
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand("""
                SELECT pid FROM pg_locks
                WHERE locktype = 'advisory' AND granted
                  AND classid::bigint = @lock_class
                  AND objid::bigint = @lock_key
                  AND objsubid = 2
                  AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                  AND pid <> pg_backend_pid()
                LIMIT 1
                """, observer);
            command.Parameters.AddWithValue("lock_class", DeploymentAdvisoryLock.ClassId);
            command.Parameters.AddWithValue("lock_key", DeploymentAdvisoryLock.KeyId);
            if (await command.ExecuteScalarAsync() is int backendPid)
                return backendPid;
            await Task.Delay(TimeSpan.FromMilliseconds(LockPollMilliseconds));
        }

        return 0;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
