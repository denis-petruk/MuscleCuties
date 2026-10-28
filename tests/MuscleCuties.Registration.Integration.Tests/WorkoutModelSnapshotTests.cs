using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class WorkoutModelSnapshotTests
{
    private const string UnusedLocalConnection = "Host=127.0.0.1;Database=mc_test_model_metadata;Username=postgres";

    [Xunit.Fact]
    public void ModelSnapshotMatchesCurrentPersistenceContracts()
    {
        using var context = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(UnusedLocalConnection).Options);
        Xunit.Assert.False(context.Database.HasPendingModelChanges(),
            "Regenerate the model snapshot and verify the hand-authored migration chain on a fresh disposable database.");
    }
}
