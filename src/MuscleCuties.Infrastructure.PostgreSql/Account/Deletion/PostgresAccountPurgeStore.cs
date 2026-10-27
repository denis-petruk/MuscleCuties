using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.UseCases.Features.Account.Deletion;
using MuscleCuties.UseCases.Features.Account.Deletion.Contracts;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;

public sealed class PostgresAccountPurgeStore(
    PostgresDbContext dbContext) : IServerAccountPurgeStore
{
    private static readonly HashSet<Type> PurgedTenantRecordTypes =
    [
        typeof(UserProfileRecord),
        typeof(CommandIdempotencyRecord),
        typeof(NutritionDiaryEventCursorRecord),
        typeof(NutritionDiaryCommandEventRecord),
        typeof(UserProfileSnapshotRecord),
        typeof(UserQuizResponseRecord),
        typeof(CycleLogRecord),
        typeof(CyclePhaseLogRecord),
        typeof(SymptomLogRecord),
        typeof(DailyReadinessLogRecord),
        typeof(HiitEvidenceCaptureChallengeRecord),
        typeof(WorkoutInjuryLogRecord), typeof(WorkoutInjuryEpisodeRecord), typeof(WorkoutInjuryPrescriptionRecord),
        typeof(WorkoutAccountPlanningStateRecord), typeof(WorkoutCompletionDayCreditRecord),
        typeof(WorkoutPlanRecord), typeof(WorkoutPlanProposalV02Record),
        typeof(WorkoutInjuryPrescriptionLinkRecord),
        typeof(WorkoutPlanRevisionV02Record),
        typeof(WorkoutDayRecord),
        typeof(WorkoutDayExerciseRecord),
        typeof(WorkoutSessionLogRecord),
        typeof(ExerciseSetLogRecord),
        typeof(WorkoutLogRecord),
        typeof(WorkoutExerciseLogRecord),
        typeof(UserExercisePreferenceRecord),
        typeof(UserMovementNoteRecord),
        typeof(DailyNutritionLogRecord),
        typeof(FoodItemEntryRecord),
        typeof(UserFoodItemRecord),
        typeof(UserMealTemplateRecord),
        typeof(UserMealTemplateEntryRecord),
        typeof(LoggedMealRecord),
        typeof(LoggedMealEntryRecord),
        typeof(RecipeRecord),
        typeof(SavedMealTemplateRecord),
        typeof(PhotoJourneyEntryRecord),
        typeof(PhotoUploadIntentRecord),
        typeof(PhotoObjectCleanupRecord)
    ];

    public async Task<AccountDeletionResult> PurgeAccountDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid non-empty user ID is required for purge.", nameof(userId));

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            AssertTenantPurgeInventoryIsComplete();
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            // FK key-share locks for new tenant writes conflict with this lock. Inventory
            // must be frozen before a purge can remove the rows which identify S3 objects.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM app.users WHERE id = {userId} FOR UPDATE", cancellationToken)
                .ConfigureAwait(false);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM app.photo_upload_intents WHERE user_id = {userId} FOR UPDATE", cancellationToken)
                .ConfigureAwait(false);

            // Phase 1: Logical Revocation & Media Inventory
            var now = DateTime.UtcNow;
            await dbContext.RefreshSessions
                .Where(r => r.UserId == userId && r.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAtUtc, now), cancellationToken)
                .ConfigureAwait(false);

            var photoRows = await dbContext.PhotoJourneyEntries.AsNoTracking()
                .Where(p => p.UserId == userId).Select(p => new { p.Id, p.ImagePath })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var intents = await dbContext.PhotoUploadIntents.AsNoTracking()
                .Where(p => p.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
            var cleanups = await dbContext.PhotoObjectCleanups.AsNoTracking()
                .Where(p => p.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
            var candidates = photoRows.Select(p => new PhotoPurgeCandidate(p.Id, p.ImagePath, now))
                .Concat(intents.Select(p => new PhotoPurgeCandidate(p.Id, p.S3Key, p.ExpiresAtUtc)))
                .Concat(cleanups.Select(p => new PhotoPurgeCandidate(p.PhotoId, p.S3Key, p.NotBeforeUtc)));
            var targets = PhotoPurgeInventory.Build(userId, candidates, now);
            var mediaKeys = new List<string>();
            foreach (var target in targets)
            {
                // Requires the integrator's restricted SECURITY DEFINER bridge. An absent
                // bridge fails this transaction before any account/media inventory is lost.
                try
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT app.stage_account_photo_purge({userId}, {target.Key}, {target.NotBeforeUtc})", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedFunction)
                { throw new InvalidOperationException("Durable account photo cleanup is not configured; account purge was not committed.", exception); }
                mediaKeys.Add(target.Key);
            }

            var purgedCount = 0;

            // Phase 2: Physical Cascading Tenant Deletion in strict reverse dependency order
            // 1. User Profile Snapshots and Quiz Responses
            purgedCount += await dbContext.UserQuizResponses
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.UserProfileSnapshots
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.UserProfiles
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 2. Cycle Observations and Symptoms
            purgedCount += await dbContext.CyclePhaseLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.SymptomLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.CycleLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 3. Readiness and Injury Logs
            purgedCount += await dbContext.HiitEvidenceCaptureChallenges.Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            purgedCount += await dbContext.WorkoutInjuryPrescriptions.Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            purgedCount += await dbContext.WorkoutInjuryPrescriptionLinks.Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            purgedCount += await dbContext.WorkoutInjuryEpisodes.Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            purgedCount += await dbContext.DailyReadinessLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.WorkoutInjuryLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 4. Workout Sessions and Exercise Logs
            purgedCount += await dbContext.WorkoutCompletionDayCredits.Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            purgedCount += await dbContext.WorkoutAccountPlanningStates.Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            purgedCount += await dbContext.WorkoutExerciseLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.ExerciseSetLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.WorkoutSessionLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.WorkoutLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 5. Workout Plans and Days
            purgedCount += await dbContext.WorkoutDayExercises
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.WorkoutDays
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.WorkoutPlanRevisionsV02
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.WorkoutPlanProposalsV02
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.WorkoutPlans
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 6. User Exercise Preferences and Notes
            purgedCount += await dbContext.UserExercisePreferences
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.UserMovementNotes
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 7. Nutrition Diary Entries and Logs
            purgedCount += await dbContext.FoodItemEntries
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.DailyNutritionLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 8. Logged Meals and Entries
            purgedCount += await dbContext.LoggedMealEntries
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.LoggedMeals
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 9. Meal Templates and Custom Food Items
            purgedCount += await dbContext.UserMealTemplateEntries
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.UserMealTemplates
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.UserFoodItems
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 10. Recipes and Saved Meal Templates
            purgedCount += await dbContext.Recipes
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.SavedMealTemplates
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 11. Photo Journey Metadata
            purgedCount += await dbContext.PhotoJourneyEntries
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.PhotoUploadIntents
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.PhotoObjectCleanups
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 12. Nutrition Events, Cursors and Command Idempotency
            purgedCount += await dbContext.NutritionDiaryCommandEvents
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.NutritionDiaryEventCursors
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.CommandIdempotencyRecords
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 13. Auth Sessions, Tokens and Mappings
            purgedCount += await dbContext.RefreshSessions
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.RevokedAccessTokens
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.SourceAccountMappings
                .Where(x => x.DestinationUserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            purgedCount += await dbContext.SourceAccountClaimNonces
                .Where(x => x.DestinationUserId == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // 14. Primary User Record
            purgedCount += await dbContext.Users
                .Where(x => x.Id == userId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            await AssertNoTenantRowsRemainAsync(userId, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new AccountDeletionResult(
                Succeeded: true,
                PurgedRecordCount: purgedCount,
                CleanedMediaKeys: mediaKeys,
                Message: "Account data purged; inventoried media is scheduled for durable cleanup.");
        }).ConfigureAwait(false);
    }

    private void AssertTenantPurgeInventoryIsComplete()
    {
        var mappedTenantTypes = dbContext.Model.GetEntityTypes()
            .Where(entity => typeof(ITenantRecord).IsAssignableFrom(entity.ClrType))
            .Select(entity => entity.ClrType)
            .ToHashSet();
        if (!mappedTenantTypes.SetEquals(PurgedTenantRecordTypes))
        {
            var missing = mappedTenantTypes.Except(PurgedTenantRecordTypes).Select(type => type.Name);
            var stale = PurgedTenantRecordTypes.Except(mappedTenantTypes).Select(type => type.Name);
            throw new InvalidOperationException(
                $"Tenant purge inventory drifted. Unclassified mapped types: [{string.Join(", ", missing)}]; " +
                $"unmapped purge types: [{string.Join(", ", stale)}].");
        }
    }

    private async Task AssertNoTenantRowsRemainAsync(Guid userId, CancellationToken cancellationToken)
    {
        var remaining = await dbContext.UserProfiles.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.CommandIdempotencyRecords.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.NutritionDiaryEventCursors.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.NutritionDiaryCommandEvents.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.UserProfileSnapshots.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.UserQuizResponses.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.CycleLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.CyclePhaseLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.SymptomLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.DailyReadinessLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutInjuryLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutInjuryEpisodes.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.HiitEvidenceCaptureChallenges.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutInjuryPrescriptionLinks.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutInjuryPrescriptions.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutPlans.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutCompletionDayCredits.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutAccountPlanningStates.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutPlanProposalsV02.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutPlanRevisionsV02.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutDays.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutDayExercises.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutSessionLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.ExerciseSetLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.WorkoutExerciseLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.UserExercisePreferences.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.UserMovementNotes.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.DailyNutritionLogs.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.FoodItemEntries.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.UserFoodItems.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.UserMealTemplates.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.UserMealTemplateEntries.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.LoggedMeals.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.LoggedMealEntries.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.Recipes.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.SavedMealTemplates.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.PhotoJourneyEntries.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.PhotoUploadIntents.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.PhotoObjectCleanups.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.RefreshSessions.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.RevokedAccessTokens.AnyAsync(x => x.UserId == userId, cancellationToken)
            || await dbContext.SourceAccountMappings.AnyAsync(x => x.DestinationUserId == userId, cancellationToken)
            || await dbContext.SourceAccountClaimNonces.AnyAsync(x => x.DestinationUserId == userId, cancellationToken)
            || await dbContext.Users.AnyAsync(x => x.Id == userId, cancellationToken);
        if (remaining)
            throw new InvalidOperationException("Account purge left tenant-owned rows behind.");
    }
}
