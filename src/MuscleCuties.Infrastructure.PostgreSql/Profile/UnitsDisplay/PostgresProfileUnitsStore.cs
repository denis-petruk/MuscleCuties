using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Profile;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.UnitsDisplay;

public sealed class PostgresProfileUnitsStore(PostgresDbContext dbContext, TimeProvider timeProvider)
    : IServerProfileUnitsStore
{
    private const string SnapshotReason = "UnitsDisplayUpdate";
    private const string MetricUnitSystem = "Metric";
    private const string ImperialUnitSystem = "Imperial";

    public async Task<ServerProfileUnitsSnapshot?> LoadAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return null;

        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => LoadTransactionalAsync(userId, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<ServerProfileUnitsSnapshot?> LoadTransactionalAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
            .ConfigureAwait(false);
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new
            {
                item.UnitSystem,
                item.BodyWeightUnit,
                item.FoodMassUnit,
                item.HeightUnit,
                item.DistanceUnit,
                item.EnergyUnit,
                item.ConcurrencyVersion
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (profile is null)
            return null;

        var units = DisplayUnits.FromStored(
            profile.UnitSystem,
            profile.BodyWeightUnit,
            profile.FoodMassUnit,
            profile.HeightUnit,
            profile.DistanceUnit,
            profile.EnergyUnit);

        return new ServerProfileUnitsSnapshot(
            units.UseMetricSystem,
            units.BodyWeightUnit,
            units.FoodMassUnit,
            units.HeightUnit,
            units.DistanceUnit,
            units.EnergyUnit,
            profile.ConcurrencyVersion);
    }

    public async Task<SaveServerProfileUnitsResult> SaveAsync(
        SaveServerProfileUnitsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.ExpectedVersion <= 0)
            return new SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus.InvalidRequest, null);

        return await dbContext.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => SaveTransactionalAsync(request, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<SaveServerProfileUnitsResult> SaveTransactionalAsync(
        SaveServerProfileUnitsRequest request,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, request.UserId, cancellationToken)
            .ConfigureAwait(false);

        var profile = await dbContext.UserProfiles
            .SingleOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus.ProfileMissing, null);
        }

        if (profile.ConcurrencyVersion != request.ExpectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new SaveServerProfileUnitsResult(
                SaveServerProfileUnitsStatus.VersionConflict,
                profile.ConcurrencyVersion);
        }

        var requestedUnits = DisplayUnits.FromInput(
            request.UseMetricSystem,
            request.BodyWeightUnit,
            request.FoodMassUnit,
            request.HeightUnit,
            request.DistanceUnit,
            request.EnergyUnit);
        if (!requestedUnits.IsSupported)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus.InvalidRequest, null);
        }

        var unitSystem = requestedUnits.UnitSystem;
        var systemChanged = !string.Equals(profile.UnitSystem, unitSystem, StringComparison.Ordinal);
        var bodyWeightUnit = request.BodyWeightUnit is null && !systemChanged
            ? profile.BodyWeightUnit
            : requestedUnits.BodyWeightUnit;
        var foodMassUnit = request.FoodMassUnit is null && !systemChanged
            ? profile.FoodMassUnit
            : requestedUnits.FoodMassUnit;
        var heightUnit = request.HeightUnit is null && !systemChanged
            ? profile.HeightUnit
            : requestedUnits.HeightUnit;
        var distanceUnit = request.DistanceUnit is null && !systemChanged
            ? profile.DistanceUnit
            : requestedUnits.DistanceUnit;
        var energyUnit = request.EnergyUnit is null && !systemChanged
            ? profile.EnergyUnit
            : requestedUnits.EnergyUnit;
        var changes = systemChanged ||
                      !string.Equals(bodyWeightUnit, profile.BodyWeightUnit, StringComparison.Ordinal) ||
                      !string.Equals(foodMassUnit, profile.FoodMassUnit, StringComparison.Ordinal) ||
                      !string.Equals(heightUnit, profile.HeightUnit, StringComparison.Ordinal) ||
                      !string.Equals(distanceUnit, profile.DistanceUnit, StringComparison.Ordinal) ||
                      !string.Equals(energyUnit, profile.EnergyUnit, StringComparison.Ordinal);

        if (!changes)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus.Saved, profile.ConcurrencyVersion);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        dbContext.UserProfileSnapshots.Add(new UserProfileSnapshotRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = request.UserId,
            SnapshotReason = SnapshotReason,
            ProfileJson = JsonSerializer.Serialize(new
            {
                profile.UnitSystem,
                profile.BodyWeightUnit,
                profile.FoodMassUnit,
                profile.HeightUnit,
                profile.DistanceUnit,
                profile.EnergyUnit
            }),
            CreatedAt = now
        });

        profile.UnitSystem = unitSystem;
        profile.BodyWeightUnit = bodyWeightUnit;
        profile.FoodMassUnit = foodMassUnit;
        profile.HeightUnit = heightUnit;
        profile.DistanceUnit = distanceUnit;
        profile.EnergyUnit = energyUnit;
        profile.UpdatedAt = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SaveServerProfileUnitsResult(
                SaveServerProfileUnitsStatus.Saved,
                profile.ConcurrencyVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return new SaveServerProfileUnitsResult(SaveServerProfileUnitsStatus.VersionConflict, null);
        }
    }
}
