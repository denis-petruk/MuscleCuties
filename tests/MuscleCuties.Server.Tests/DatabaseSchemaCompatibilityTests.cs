using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>
/// Deployment-order guard for the 2026-10-29 incident: code served while the database was 28
/// migrations behind, and connectivity-only readiness still reported Healthy.
/// </summary>
public sealed class DatabaseSchemaCompatibilityTests
{
    private const string FirstMigration = "20261017003443_InitialPostgreSqlApplication";
    private const string SecondMigration = "20261026220000_AddWorkoutSafetySuppression";
    private const string ThirdMigration = "20261028190000_AddWorkoutCycleReadinessOccurrences";
    private const int OneMigration = 1;
    private const int TwoMigrations = 2;

    [Fact]
    public void LedgerMatchingTheDeployedAssemblyIsCompatible()
    {
        var result = DatabaseSchemaCompatibility.Evaluate(
            [ThirdMigration, FirstMigration, SecondMigration],
            [FirstMigration, SecondMigration, ThirdMigration]);

        Assert.True(result.IsCompatible);
        Assert.Equal(0, result.PendingMigrationCount);
        Assert.Equal(0, result.UnknownAppliedMigrationCount);
    }

    [Fact]
    public void EmptyLedgerReportsEveryCompiledMigrationAsPending()
    {
        var result = DatabaseSchemaCompatibility.Evaluate(
            [],
            [FirstMigration, SecondMigration, ThirdMigration]);

        Assert.False(result.IsCompatible);
        Assert.Equal(3, result.PendingMigrationCount);
        Assert.Equal(0, result.UnknownAppliedMigrationCount);
    }

    [Fact]
    public void DatabaseBehindTheDeployedAssemblyIsIncompatible()
    {
        var result = DatabaseSchemaCompatibility.Evaluate(
            [FirstMigration],
            [FirstMigration, SecondMigration, ThirdMigration]);

        Assert.False(result.IsCompatible);
        Assert.Equal(TwoMigrations, result.PendingMigrationCount);
        Assert.Equal(0, result.UnknownAppliedMigrationCount);
    }

    [Fact]
    public void DatabaseAheadOfTheDeployedAssemblyIsIncompatible()
    {
        var result = DatabaseSchemaCompatibility.Evaluate(
            [FirstMigration, SecondMigration, ThirdMigration],
            [FirstMigration, SecondMigration]);

        Assert.False(result.IsCompatible);
        Assert.Equal(0, result.PendingMigrationCount);
        Assert.Equal(OneMigration, result.UnknownAppliedMigrationCount);
    }

    [Fact]
    public void DivergentLedgerAndAssemblyAreIncompatible()
    {
        var result = DatabaseSchemaCompatibility.Evaluate(
            [FirstMigration, SecondMigration],
            [SecondMigration, ThirdMigration]);

        Assert.False(result.IsCompatible);
        Assert.Equal(OneMigration, result.PendingMigrationCount);
        Assert.Equal(OneMigration, result.UnknownAppliedMigrationCount);
    }

    [Fact]
    public void DuplicateLedgerEntriesDoNotChangeCompatibility()
    {
        var result = DatabaseSchemaCompatibility.Evaluate(
            [FirstMigration, FirstMigration, SecondMigration],
            [FirstMigration, SecondMigration]);

        Assert.True(result.IsCompatible);
    }
}
