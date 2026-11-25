using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Models.Operations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

/// <summary>The one DbContext for the server database. Three schemas separate concerns (see <see cref="PostgresSchemas"/>):
/// app for per-account rows, catalog for seeded reference data, ops for operational records.</summary>
public sealed class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
{
    /// <summary>Accounts, keyed by the local user id every other app table references.</summary>
    public DbSet<UserRecord> Users => Set<UserRecord>();
    /// <summary>Outstanding email verification codes, short-lived and consumed on use.</summary>
    public DbSet<EmailChallengeRecord> EmailChallenges => Set<EmailChallengeRecord>();
    /// <summary>Issued refresh tokens, so one can be rotated or revoked without touching the others.</summary>
    public DbSet<RefreshSessionRecord> RefreshSessions => Set<RefreshSessionRecord>();
    /// <summary>One-time nonces that stop an Apple identity token being replayed.</summary>
    public DbSet<AppleSignInNonceRecord> AppleSignInNonces => Set<AppleSignInNonceRecord>();
    /// <summary>Access tokens revoked before expiry, checked on every authenticated call.</summary>
    public DbSet<RevokedAccessTokenRecord> RevokedAccessTokens => Set<RevokedAccessTokenRecord>();
    /// <summary>Email change challenges, kept apart from sign-in challenges so the two flows cannot cross-confirm.</summary>
    public DbSet<EmailChallengeSessionRecord> EmailChallengeSessions => Set<EmailChallengeSessionRecord>();
    /// <summary>One-time nonces for claiming a pre-existing account during migration.</summary>
    public DbSet<SourceAccountClaimNonceRecord> SourceAccountClaimNonces => Set<SourceAccountClaimNonceRecord>();
    /// <summary>Which source account a migrated row came from, so an import is never applied twice.</summary>
    public DbSet<SourceAccountMappingRecord> SourceAccountMappings => Set<SourceAccountMappingRecord>();
    /// <summary>The profile, one row per account, carrying the optimistic-concurrency version the context bumps on save.</summary>
    public DbSet<UserProfileRecord> UserProfiles => Set<UserProfileRecord>();
    /// <summary>Recorded command keys, so a retried write returns the first result instead of applying twice.</summary>
    public DbSet<CommandIdempotencyRecord> CommandIdempotencyRecords => Set<CommandIdempotencyRecord>();
    /// <summary>How far each device has consumed the diary event stream.</summary>
    public DbSet<NutritionDiaryEventCursorRecord> NutritionDiaryEventCursors => Set<NutritionDiaryEventCursorRecord>();
    /// <summary>The diary's append-only command log the cursor reads.</summary>
    public DbSet<NutritionDiaryCommandEventRecord> NutritionDiaryCommandEvents => Set<NutritionDiaryCommandEventRecord>();
    /// <summary>Point-in-time profile copies, written when a plan needs the inputs it was generated from.</summary>
    public DbSet<UserProfileSnapshotRecord> UserProfileSnapshots => Set<UserProfileSnapshotRecord>();
    /// <summary>Onboarding answers, kept separate from the profile they were folded into.</summary>
    public DbSet<UserQuizResponseRecord> UserQuizResponses => Set<UserQuizResponseRecord>();
    /// <summary>Recorded cycles, the anchor the phase projection counts from.</summary>
    public DbSet<CycleLogRecord> CycleLogs => Set<CycleLogRecord>();
    /// <summary>Per-day phase records, which override the projection for the days she logged.</summary>
    public DbSet<CyclePhaseLogRecord> CyclePhaseLogs => Set<CyclePhaseLogRecord>();
    /// <summary>Symptoms per day, tied to the cycle they were logged in.</summary>
    public DbSet<SymptomLogRecord> SymptomLogs => Set<SymptomLogRecord>();
    /// <summary>Daily readiness answers, which gate whether a strong session is offered.</summary>
    public DbSet<DailyReadinessLogRecord> DailyReadinessLogs => Set<DailyReadinessLogRecord>();
    /// <summary>Reported injuries and their status, the input that withholds unsafe sessions.</summary>
    public DbSet<WorkoutInjuryLogRecord> WorkoutInjuryLogs => Set<WorkoutInjuryLogRecord>();
    /// <summary>Evidence challenges a HIIT capture must answer before its numbers are accepted.</summary>
    public DbSet<HiitEvidenceCaptureChallengeRecord> HiitEvidenceCaptureChallenges => Set<HiitEvidenceCaptureChallengeRecord>();
    /// <summary>Injury episodes, grouped so a re-report continues one episode instead of starting another.</summary>
    public DbSet<WorkoutInjuryEpisodeRecord> WorkoutInjuryEpisodes => Set<WorkoutInjuryEpisodeRecord>();
    /// <summary>What each episode restricts, applied to planning as a documented rule rather than an ad-hoc filter.</summary>
    public DbSet<WorkoutInjuryPrescriptionRecord> WorkoutInjuryPrescriptions => Set<WorkoutInjuryPrescriptionRecord>();
    /// <summary>Which exercises or slots a prescription applies to.</summary>
    public DbSet<WorkoutInjuryPrescriptionLinkRecord> WorkoutInjuryPrescriptionLinks => Set<WorkoutInjuryPrescriptionLinkRecord>();
    /// <summary>Slots withheld from a plan because of an injury, with the review reason the API returns.</summary>
    public DbSet<WorkoutInjuryOmittedSlotRecord> WorkoutInjuryOmittedSlots => Set<WorkoutInjuryOmittedSlotRecord>();
    /// <summary>The strength style offered to an account, and whether it was accepted or dismissed.</summary>
    public DbSet<StrengthStyleOfferRecord> StrengthStyleOffers => Set<StrengthStyleOfferRecord>();
    /// <summary>The plan in force; replacing it is the one write path that changes what she trains.</summary>
    public DbSet<WorkoutPlanRecord> WorkoutPlans => Set<WorkoutPlanRecord>();
    /// <summary>Per-account planning state: the lease and the proposal the account is currently on.</summary>
    public DbSet<WorkoutAccountPlanningStateRecord> WorkoutAccountPlanningStates => Set<WorkoutAccountPlanningStateRecord>();
    /// <summary>Readiness occurrences attributed to a cycle, so the same day is not counted twice.</summary>
    public DbSet<WorkoutCycleReadinessOccurrenceRecord> WorkoutCycleReadinessOccurrences => Set<WorkoutCycleReadinessOccurrenceRecord>();
    /// <summary>The credit a completed day earns towards the weekly volume budget.</summary>
    public DbSet<WorkoutCompletionDayCreditRecord> WorkoutCompletionDayCredits => Set<WorkoutCompletionDayCreditRecord>();
    /// <summary>Version-2 plan proposals, kept until they are applied or superseded.</summary>
    public DbSet<WorkoutPlanProposalV02Record> WorkoutPlanProposalsV02 => Set<WorkoutPlanProposalV02Record>();
    /// <summary>Applied version-2 revisions, so an applied plan can be traced back to what was proposed.</summary>
    public DbSet<WorkoutPlanRevisionV02Record> WorkoutPlanRevisionsV02 => Set<WorkoutPlanRevisionV02Record>();
    /// <summary>The days of a plan, ordered within it.</summary>
    public DbSet<WorkoutDayRecord> WorkoutDays => Set<WorkoutDayRecord>();
    /// <summary>The prescribed exercises of a day, with their sets and target effort.</summary>
    public DbSet<WorkoutDayExerciseRecord> WorkoutDayExercises => Set<WorkoutDayExerciseRecord>();
    /// <summary>Sessions performed, which is the evidence the plan's progression is judged on.</summary>
    public DbSet<WorkoutSessionLogRecord> WorkoutSessionLogs => Set<WorkoutSessionLogRecord>();
    /// <summary>Per-set results within a session.</summary>
    public DbSet<ExerciseSetLogRecord> ExerciseSetLogs => Set<ExerciseSetLogRecord>();
    /// <summary>Legacy per-workout log rows, kept readable alongside the session logs.</summary>
    public DbSet<WorkoutLogRecord> WorkoutLogs => Set<WorkoutLogRecord>();
    /// <summary>Legacy per-exercise rows belonging to a workout log.</summary>
    public DbSet<WorkoutExerciseLogRecord> WorkoutExerciseLogs => Set<WorkoutExerciseLogRecord>();
    /// <summary>Per-account exercise preferences the planner respects.</summary>
    public DbSet<UserExercisePreferenceRecord> UserExercisePreferences => Set<UserExercisePreferenceRecord>();
    /// <summary>Her own notes on a movement, shown next to the prescription.</summary>
    public DbSet<UserMovementNoteRecord> UserMovementNotes => Set<UserMovementNoteRecord>();
    /// <summary>One row per account and day, the header the day's entries hang from.</summary>
    public DbSet<DailyNutritionLogRecord> DailyNutritionLogs => Set<DailyNutritionLogRecord>();
    /// <summary>The food entries of a day, each carrying the grams it was eaten at.</summary>
    public DbSet<FoodItemEntryRecord> FoodItemEntries => Set<FoodItemEntryRecord>();
    /// <summary>Foods an account created, kept apart from the shared catalog so they never leak into it.</summary>
    public DbSet<UserFoodItemRecord> UserFoodItems => Set<UserFoodItemRecord>();
    /// <summary>An account's own meal templates.</summary>
    public DbSet<UserMealTemplateRecord> UserMealTemplates => Set<UserMealTemplateRecord>();
    /// <summary>The foods inside one of an account's meal templates.</summary>
    public DbSet<UserMealTemplateEntryRecord> UserMealTemplateEntries => Set<UserMealTemplateEntryRecord>();
    /// <summary>Meals logged on a day, the parent of their entries.</summary>
    public DbSet<LoggedMealRecord> LoggedMeals => Set<LoggedMealRecord>();
    /// <summary>The foods inside a logged meal.</summary>
    public DbSet<LoggedMealEntryRecord> LoggedMealEntries => Set<LoggedMealEntryRecord>();
    /// <summary>Saved recipes, whose contents are stored as a JSON snapshot so the shape can grow without a migration.</summary>
    public DbSet<RecipeRecord> Recipes => Set<RecipeRecord>();
    /// <summary>Saved meal templates she can log again with one tap.</summary>
    public DbSet<SavedMealTemplateRecord> SavedMealTemplates => Set<SavedMealTemplateRecord>();
    /// <summary>Progress photos by date, holding the object key rather than the image.</summary>
    public DbSet<PhotoJourneyEntryRecord> PhotoJourneyEntries => Set<PhotoJourneyEntryRecord>();
    /// <summary>Uploads that were authorised but not yet confirmed, so an abandoned upload can be cleaned up.</summary>
    public DbSet<PhotoUploadIntentRecord> PhotoUploadIntents => Set<PhotoUploadIntentRecord>();
    /// <summary>Photo objects queued for deletion, drained by the cleanup worker.</summary>
    public DbSet<PhotoObjectCleanupRecord> PhotoObjectCleanups => Set<PhotoObjectCleanupRecord>();
    /// <summary>Objects the reconciler must confirm against the bucket.</summary>
    public DbSet<PhotoObjectReconcileTargetRecord> PhotoObjectReconcileTargets => Set<PhotoObjectReconcileTargetRecord>();

    /// <summary>Quiz questions from the shared catalog, ordered by their index.</summary>
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    /// <summary>Answer options, each mapped to the profile value it stores.</summary>
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    /// <summary>The shared food catalog, separate from an account's own foods.</summary>
    public DbSet<FoodItem> CatalogFoodItems => Set<FoodItem>();
    /// <summary>The shared meal templates the catalog ships.</summary>
    public DbSet<MealTemplate> CatalogMealTemplates => Set<MealTemplate>();
    /// <summary>The foods inside a shared meal template.</summary>
    public DbSet<MealTemplateEntry> CatalogMealTemplateEntries => Set<MealTemplateEntry>();
    /// <summary>The shared exercise catalog the app browses.</summary>
    public DbSet<Exercise> Exercises => Set<Exercise>();
    /// <summary>The muscle groups volume and coverage are measured in.</summary>
    public DbSet<WorkoutMuscleGroup> WorkoutMuscleGroups => Set<WorkoutMuscleGroup>();
    /// <summary>How strongly each muscle group is weighted for each goal and tier.</summary>
    public DbSet<GoalTierWeight> GoalTierWeights => Set<GoalTierWeight>();
    /// <summary>The planning definition of an exercise: its pattern, cost and reviewed restriction tags.</summary>
    public DbSet<WorkoutExerciseDefinition> WorkoutExerciseDefinitions => Set<WorkoutExerciseDefinition>();
    /// <summary>How much of an exercise's work falls on each muscle group.</summary>
    public DbSet<ExerciseMuscleContribution> ExerciseMuscleContributions => Set<ExerciseMuscleContribution>();
    /// <summary>The session shapes a week is built from.</summary>
    public DbSet<SessionArchetype> SessionArchetypes => Set<SessionArchetype>();
    /// <summary>The slots inside a session archetype, with their set and rep ranges.</summary>
    public DbSet<SlotTemplate> SlotTemplates => Set<SlotTemplate>();
    /// <summary>The archetype sequence for each number of training days.</summary>
    public DbSet<WeekTemplate> WeekTemplates => Set<WeekTemplate>();
    /// <summary>The weekly fractional-set budget per muscle group for each training frequency.</summary>
    public DbSet<VolumeBudgetRow> VolumeBudgetRows => Set<VolumeBudgetRow>();
    /// <summary>The planner's tunable thresholds, keyed by section and name.</summary>
    public DbSet<WorkoutPlanningConfigEntry> WorkoutPlanningConfigEntries => Set<WorkoutPlanningConfigEntry>();
    /// <summary>Past nutrient snapshots of a food, so a sync's change can be traced.</summary>
    public DbSet<FoodItemVersionRecord> FoodItemVersions => Set<FoodItemVersionRecord>();
    /// <summary>The record of catalog sync runs and what each changed.</summary>
    public DbSet<FoodSyncLogRecord> FoodSyncLogs => Set<FoodSyncLogRecord>();

    /// <summary>Applies the column types every model shares: instants are stored as <c>timestamp with time zone</c> and decimals
    /// as <c>numeric</c>, so no property can silently fall back to a timezone-naive or floating-point type.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp with time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp with time zone");
        configurationBuilder.Properties<decimal>().HaveColumnType("numeric");
        configurationBuilder.Properties<decimal?>().HaveColumnType("numeric");
    }

    /// <summary>Puts every table in the app schema unless a configuration moves it, then applies the configurations from this
    /// assembly — the one place mappings are discovered.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PostgresSchemas.App);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgresDbContext).Assembly);
    }

    /// <summary>Bumps the profile's concurrency version on every update before saving, which is what makes a stale edit fail
    /// instead of overwriting a newer one.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<UserProfileRecord>().Where(entry => entry.State == EntityState.Modified))
            entry.Entity.ConcurrencyVersion = checked(entry.Entity.ConcurrencyVersion + 1);

        return base.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>The schema names, declared once because every configuration, migration and purge query refers to them.</summary>
public static class PostgresSchemas
{
    /// <summary>Per-account tables, the schema every account-scoped query targets.</summary>
    public const string App = "app";
    /// <summary>Seeded reference data shared by all accounts.</summary>
    public const string Catalog = "catalog";
    /// <summary>Operational rows (sync logs, idempotency) that no user read touches.</summary>
    public const string Operations = "ops";
}
