using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Models.Operations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

public sealed class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : DbContext(options)
{
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<EmailChallengeRecord> EmailChallenges => Set<EmailChallengeRecord>();
    public DbSet<RefreshSessionRecord> RefreshSessions => Set<RefreshSessionRecord>();
    public DbSet<AppleSignInNonceRecord> AppleSignInNonces => Set<AppleSignInNonceRecord>();
    public DbSet<RevokedAccessTokenRecord> RevokedAccessTokens => Set<RevokedAccessTokenRecord>();
    public DbSet<EmailChallengeSessionRecord> EmailChallengeSessions => Set<EmailChallengeSessionRecord>();
    public DbSet<SourceAccountClaimNonceRecord> SourceAccountClaimNonces => Set<SourceAccountClaimNonceRecord>();
    public DbSet<SourceAccountMappingRecord> SourceAccountMappings => Set<SourceAccountMappingRecord>();
    public DbSet<UserProfileRecord> UserProfiles => Set<UserProfileRecord>();
    public DbSet<CommandIdempotencyRecord> CommandIdempotencyRecords => Set<CommandIdempotencyRecord>();
    public DbSet<NutritionDiaryEventCursorRecord> NutritionDiaryEventCursors => Set<NutritionDiaryEventCursorRecord>();
    public DbSet<NutritionDiaryCommandEventRecord> NutritionDiaryCommandEvents => Set<NutritionDiaryCommandEventRecord>();
    public DbSet<UserProfileSnapshotRecord> UserProfileSnapshots => Set<UserProfileSnapshotRecord>();
    public DbSet<UserQuizResponseRecord> UserQuizResponses => Set<UserQuizResponseRecord>();
    public DbSet<CycleLogRecord> CycleLogs => Set<CycleLogRecord>();
    public DbSet<CyclePhaseLogRecord> CyclePhaseLogs => Set<CyclePhaseLogRecord>();
    public DbSet<SymptomLogRecord> SymptomLogs => Set<SymptomLogRecord>();
    public DbSet<DailyReadinessLogRecord> DailyReadinessLogs => Set<DailyReadinessLogRecord>();
    public DbSet<WorkoutInjuryLogRecord> WorkoutInjuryLogs => Set<WorkoutInjuryLogRecord>();
    public DbSet<HiitEvidenceCaptureChallengeRecord> HiitEvidenceCaptureChallenges => Set<HiitEvidenceCaptureChallengeRecord>();
    public DbSet<WorkoutInjuryEpisodeRecord> WorkoutInjuryEpisodes => Set<WorkoutInjuryEpisodeRecord>();
    public DbSet<WorkoutInjuryPrescriptionRecord> WorkoutInjuryPrescriptions => Set<WorkoutInjuryPrescriptionRecord>();
    public DbSet<WorkoutInjuryPrescriptionLinkRecord> WorkoutInjuryPrescriptionLinks => Set<WorkoutInjuryPrescriptionLinkRecord>();
    public DbSet<WorkoutInjuryOmittedSlotRecord> WorkoutInjuryOmittedSlots => Set<WorkoutInjuryOmittedSlotRecord>();
    public DbSet<StrengthStyleOfferRecord> StrengthStyleOffers => Set<StrengthStyleOfferRecord>();
    public DbSet<WorkoutPlanRecord> WorkoutPlans => Set<WorkoutPlanRecord>();
    public DbSet<WorkoutAccountPlanningStateRecord> WorkoutAccountPlanningStates => Set<WorkoutAccountPlanningStateRecord>();
    public DbSet<WorkoutCycleReadinessOccurrenceRecord> WorkoutCycleReadinessOccurrences => Set<WorkoutCycleReadinessOccurrenceRecord>();
    public DbSet<WorkoutCompletionDayCreditRecord> WorkoutCompletionDayCredits => Set<WorkoutCompletionDayCreditRecord>();
    public DbSet<WorkoutPlanProposalV02Record> WorkoutPlanProposalsV02 => Set<WorkoutPlanProposalV02Record>();
    public DbSet<WorkoutPlanRevisionV02Record> WorkoutPlanRevisionsV02 => Set<WorkoutPlanRevisionV02Record>();
    public DbSet<WorkoutDayRecord> WorkoutDays => Set<WorkoutDayRecord>();
    public DbSet<WorkoutDayExerciseRecord> WorkoutDayExercises => Set<WorkoutDayExerciseRecord>();
    public DbSet<WorkoutSessionLogRecord> WorkoutSessionLogs => Set<WorkoutSessionLogRecord>();
    public DbSet<ExerciseSetLogRecord> ExerciseSetLogs => Set<ExerciseSetLogRecord>();
    public DbSet<WorkoutLogRecord> WorkoutLogs => Set<WorkoutLogRecord>();
    public DbSet<WorkoutExerciseLogRecord> WorkoutExerciseLogs => Set<WorkoutExerciseLogRecord>();
    public DbSet<UserExercisePreferenceRecord> UserExercisePreferences => Set<UserExercisePreferenceRecord>();
    public DbSet<UserMovementNoteRecord> UserMovementNotes => Set<UserMovementNoteRecord>();
    public DbSet<DailyNutritionLogRecord> DailyNutritionLogs => Set<DailyNutritionLogRecord>();
    public DbSet<FoodItemEntryRecord> FoodItemEntries => Set<FoodItemEntryRecord>();
    public DbSet<UserFoodItemRecord> UserFoodItems => Set<UserFoodItemRecord>();
    public DbSet<UserMealTemplateRecord> UserMealTemplates => Set<UserMealTemplateRecord>();
    public DbSet<UserMealTemplateEntryRecord> UserMealTemplateEntries => Set<UserMealTemplateEntryRecord>();
    public DbSet<LoggedMealRecord> LoggedMeals => Set<LoggedMealRecord>();
    public DbSet<LoggedMealEntryRecord> LoggedMealEntries => Set<LoggedMealEntryRecord>();
    public DbSet<RecipeRecord> Recipes => Set<RecipeRecord>();
    public DbSet<SavedMealTemplateRecord> SavedMealTemplates => Set<SavedMealTemplateRecord>();
    public DbSet<PhotoJourneyEntryRecord> PhotoJourneyEntries => Set<PhotoJourneyEntryRecord>();
    public DbSet<PhotoUploadIntentRecord> PhotoUploadIntents => Set<PhotoUploadIntentRecord>();
    public DbSet<PhotoObjectCleanupRecord> PhotoObjectCleanups => Set<PhotoObjectCleanupRecord>();
    public DbSet<PhotoObjectReconcileTargetRecord> PhotoObjectReconcileTargets => Set<PhotoObjectReconcileTargetRecord>();

    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    public DbSet<FoodItem> CatalogFoodItems => Set<FoodItem>();
    public DbSet<MealTemplate> CatalogMealTemplates => Set<MealTemplate>();
    public DbSet<MealTemplateEntry> CatalogMealTemplateEntries => Set<MealTemplateEntry>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutMuscleGroup> WorkoutMuscleGroups => Set<WorkoutMuscleGroup>();
    public DbSet<GoalTierWeight> GoalTierWeights => Set<GoalTierWeight>();
    public DbSet<WorkoutExerciseDefinition> WorkoutExerciseDefinitions => Set<WorkoutExerciseDefinition>();
    public DbSet<ExerciseMuscleContribution> ExerciseMuscleContributions => Set<ExerciseMuscleContribution>();
    public DbSet<SessionArchetype> SessionArchetypes => Set<SessionArchetype>();
    public DbSet<SlotTemplate> SlotTemplates => Set<SlotTemplate>();
    public DbSet<WeekTemplate> WeekTemplates => Set<WeekTemplate>();
    public DbSet<VolumeBudgetRow> VolumeBudgetRows => Set<VolumeBudgetRow>();
    public DbSet<WorkoutPlanningConfigEntry> WorkoutPlanningConfigEntries => Set<WorkoutPlanningConfigEntry>();
    public DbSet<FoodItemVersionRecord> FoodItemVersions => Set<FoodItemVersionRecord>();
    public DbSet<FoodSyncLogRecord> FoodSyncLogs => Set<FoodSyncLogRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp with time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp with time zone");
        configurationBuilder.Properties<decimal>().HaveColumnType("numeric");
        configurationBuilder.Properties<decimal?>().HaveColumnType("numeric");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PostgresSchemas.App);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgresDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<UserProfileRecord>().Where(entry => entry.State == EntityState.Modified))
            entry.Entity.ConcurrencyVersion = checked(entry.Entity.ConcurrencyVersion + 1);

        return base.SaveChangesAsync(cancellationToken);
    }
}

public static class PostgresSchemas
{
    public const string App = "app";
    public const string Catalog = "catalog";
    public const string Operations = "ops";
}
