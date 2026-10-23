using Microsoft.EntityFrameworkCore;
using System.Data;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Cycle.History;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Cycle.History;

public sealed class PostgresCycleHistoryStore(PostgresDbContext dbContext, TimeProvider timeProvider) : ICycleHistoryStore
{
    private const int UnknownCycleLength = 0;
    public async Task<CyclePredictionInputs> LoadPredictionInputsAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var cycles = await dbContext.CycleLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.StartDate <= today)
                .OrderBy(row => row.StartDate)
                .Select(row => new CycleObservation(row.Id, row.StartDate, row.EndDate, row.CycleLength))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var profileCycleLength = await dbContext.UserProfiles.AsNoTracking()
                .Where(row => row.UserId == userId)
                .Select(row => (int?)row.CycleLength)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var phaseLog = await dbContext.CyclePhaseLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.LocalDate <= today)
                .OrderByDescending(row => row.LocalDate).ThenByDescending(row => row.Id)
                .Select(row => new CycleManualPhaseLog(row.Phase, row.LocalDate))
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CyclePredictionInputs(cycles, profileCycleLength, phaseLog);
        }).ConfigureAwait(false);
    }

    public async Task<CycleHistorySnapshot> LoadAsync(Guid userId, DateOnly from, DateOnly through, CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
        var cycles = await dbContext.CycleLogs.AsNoTracking().Where(row => row.UserId == userId && row.StartDate <= through && (row.EndDate == null || row.EndDate >= from))
            .OrderBy(row => row.StartDate).Select(row => new CycleObservation(row.Id, row.StartDate, row.EndDate, row.CycleLength)).ToListAsync(cancellationToken).ConfigureAwait(false);
        var symptoms = await dbContext.SymptomLogs.AsNoTracking().Where(row => row.UserId == userId && row.Date >= from && row.Date <= through)
            .OrderBy(row => row.Date).Select(row => new CycleSymptom(row.Id, row.CycleLogId, row.Date, row.SymptomType, row.Severity, row.Notes)).ToListAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CycleHistorySnapshot(cycles, symptoms);
        }).ConfigureAwait(false);
    }

    public async Task<CycleObservation?> SaveCycleAsync(SaveCycleObservation request, CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, request.UserId, cancellationToken).ConfigureAwait(false);
        await LockRecordIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        CycleLogRecord? row = await dbContext.CycleLogs.SingleOrDefaultAsync(item => item.UserId == request.UserId && item.Id == request.Id, cancellationToken).ConfigureAwait(false);
        row ??= new CycleLogRecord { Id = request.Id, UserId = request.UserId, CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
        row.StartDate = request.StartDate;
        row.EndDate = request.EndDate;
        // Cycle length is unknown until another cycle start is observed. EndDate is not a cycle interval.
        row.CycleLength = UnknownCycleLength;
        if (dbContext.Entry(row).State == EntityState.Detached) dbContext.CycleLogs.Add(row);
        try { await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException exception) when (IsUniqueIdCollision(exception))
        { throw new CycleHistoryIdConflictException(); }
        await RecalculateCycleLengthsFromStartsAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CycleObservation(row.Id, row.StartDate, row.EndDate, row.CycleLength);
        }).ConfigureAwait(false);
    }

    public async Task<CycleSymptom?> SaveSymptomAsync(SaveCycleSymptom request, CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, request.UserId, cancellationToken).ConfigureAwait(false);
        await LockRecordIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        var cycleExists = await dbContext.CycleLogs.AnyAsync(row => row.UserId == request.UserId && row.Id == request.CycleId, cancellationToken).ConfigureAwait(false);
        if (!cycleExists) return await Rollback<CycleSymptom>(transaction).ConfigureAwait(false);
        SymptomLogRecord? row = await dbContext.SymptomLogs.SingleOrDefaultAsync(item => item.UserId == request.UserId && item.Id == request.Id, cancellationToken).ConfigureAwait(false);
        row ??= new SymptomLogRecord { Id = request.Id, UserId = request.UserId, CreatedAt = timeProvider.GetUtcNow().UtcDateTime };
        row.CycleLogId = request.CycleId; row.Date = request.Date; row.SymptomType = request.Type; row.Severity = request.Severity; row.Notes = request.Notes;
        if (dbContext.Entry(row).State == EntityState.Detached) dbContext.SymptomLogs.Add(row);
        try { await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException exception) when (IsUniqueIdCollision(exception))
        { throw new CycleHistoryIdConflictException(); }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new CycleSymptom(row.Id, row.CycleLogId, row.Date, row.SymptomType, row.Severity, row.Notes);
        }).ConfigureAwait(false);
    }

    public async Task<bool> DeleteSymptomAsync(Guid userId, Guid symptomId, CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
        var row = await dbContext.SymptomLogs.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == symptomId, cancellationToken).ConfigureAwait(false);
        if (row is null) { await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return false; }
        dbContext.SymptomLogs.Remove(row); await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return true;
        }).ConfigureAwait(false);
    }

    public async Task<bool> DeleteCycleAsync(Guid userId, Guid cycleId, CancellationToken cancellationToken)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var row = await dbContext.CycleLogs.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == cycleId, cancellationToken).ConfigureAwait(false);
            if (row is null) { await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return false; }
            var symptoms = await dbContext.SymptomLogs.Where(item => item.UserId == userId && item.CycleLogId == cycleId).ToListAsync(cancellationToken).ConfigureAwait(false);
            dbContext.SymptomLogs.RemoveRange(symptoms);
            dbContext.CycleLogs.Remove(row);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await RecalculateCycleLengthsFromStartsAsync(userId, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    private async Task RecalculateCycleLengthsFromStartsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cycles = await dbContext.CycleLogs
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < cycles.Count; index++)
        {
            cycles[index].CycleLength = index + 1 < cycles.Count && cycles[index + 1].StartDate > cycles[index].StartDate
                ? cycles[index + 1].StartDate.DayNumber - cycles[index].StartDate.DayNumber
                : UnknownCycleLength;
        }
    }

    private Task<int> LockRecordIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString("D")}, 0))", cancellationToken);

    private static bool IsUniqueIdCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static async Task<T?> Rollback<T>(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) where T : class
    { await transaction.RollbackAsync().ConfigureAwait(false); return null; }
}
