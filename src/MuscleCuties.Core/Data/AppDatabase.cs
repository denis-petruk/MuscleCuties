using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Data.Sqlite;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase : DbContext
{
    public AppDatabase(DbContextOptions<AppDatabase> options) : base(options) { }

    // User domain
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserProfileSnapshot> UserProfileSnapshots => Set<UserProfileSnapshot>();

    // Quiz domain
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    public DbSet<UserQuizResponse> UserQuizResponses => Set<UserQuizResponse>();

    // Cycle domain
    public DbSet<CycleLog> CycleLogs => Set<CycleLog>();
    public DbSet<CyclePhaseLog> CyclePhaseLogs => Set<CyclePhaseLog>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();

    // Nutrition domain
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<FoodItemVersion> FoodItemVersions => Set<FoodItemVersion>();
    public DbSet<FoodSyncLog> FoodSyncLogs => Set<FoodSyncLog>();
    public DbSet<MealTemplate> MealTemplates => Set<MealTemplate>();
    public DbSet<MealTemplateEntry> MealTemplateEntries => Set<MealTemplateEntry>();
    public DbSet<LoggedMeal> LoggedMeals => Set<LoggedMeal>();
    public DbSet<LoggedMealEntry> LoggedMealEntries => Set<LoggedMealEntry>();

    // Workout domain
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutDay> WorkoutDays => Set<WorkoutDay>();
    public DbSet<WorkoutDayExercise> WorkoutDayExercises => Set<WorkoutDayExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<WorkoutExerciseLog> WorkoutExerciseLogs => Set<WorkoutExerciseLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUserDomain(modelBuilder);
        ConfigureQuizDomain(modelBuilder);
        ConfigureCycleDomain(modelBuilder);
        ConfigureNutritionDomain(modelBuilder);
        ConfigureWorkoutDomain(modelBuilder);
    }

    public async Task InitializeAsync()
    {
        await Database.EnsureCreatedAsync();
        await SqliteSchemaMaintenance.RepairAsync(this);
        await SeedReferenceDataAsync();
    }

    private static void ConfigureUserDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(320);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);

            entity.HasOne(u => u.UserProfile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasIndex(p => p.UserId).IsUnique();
            entity.Property(p => p.CycleTrackingMode).HasConversion<int>();
            entity.Property(p => p.Name).IsRequired().HasMaxLength(120);
            entity.Property(p => p.DietaryTags).HasMaxLength(250);
            entity.Property(p => p.PreferredWorkoutActivityTypes).HasMaxLength(500);
            entity.Property(p => p.UnitSystem).IsRequired().HasMaxLength(20);
            entity.Property(p => p.BodyWeightUnit).IsRequired().HasMaxLength(12);
            entity.Property(p => p.FoodMassUnit).IsRequired().HasMaxLength(12);
            entity.Property(p => p.HeightUnit).IsRequired().HasMaxLength(12);
            entity.Property(p => p.DistanceUnit).IsRequired().HasMaxLength(12);
            entity.Property(p => p.EnergyUnit).IsRequired().HasMaxLength(12);
            entity.Property(p => p.NutritionGoalsJson).HasMaxLength(4000);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_UserProfile_Height", "Height >= 0");
                t.HasCheckConstraint("CK_UserProfile_Weight", "Weight >= 0");
                t.HasCheckConstraint("CK_UserProfile_TrainingExperienceLevel", "TrainingExperienceLevel >= 0 AND TrainingExperienceLevel <= 3");
                t.HasCheckConstraint("CK_UserProfile_CycleTrackingMode", "CycleTrackingMode >= 0 AND CycleTrackingMode <= 3");
                t.HasCheckConstraint("CK_UserProfile_WorkoutDays", "WorkoutDaysPerWeek >= 0 AND WorkoutDaysPerWeek <= 7");
                t.HasCheckConstraint("CK_UserProfile_CycleLength", "CycleLength >= 0 AND CycleLength <= 60");
            });
        });

        modelBuilder.Entity<UserProfileSnapshot>(entity =>
        {
            entity.HasIndex(s => new { s.UserId, s.CreatedAt });
            entity.Property(s => s.SnapshotReason).IsRequired().HasMaxLength(40);
            entity.Property(s => s.ProfileJson).IsRequired();
            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureQuizDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuizQuestion>(entity =>
        {
            entity.HasIndex(q => q.OrderIndex).IsUnique();
            entity.Property(q => q.Question).IsRequired().HasMaxLength(250);
            entity.HasMany(q => q.Answers)
                .WithOne(a => a.Question)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuizAnswer>(entity =>
        {
            entity.HasIndex(a => new { a.QuestionId, a.OrderIndex }).IsUnique();
            entity.Property(a => a.Text).IsRequired().HasMaxLength(160);
        });

        modelBuilder.Entity<UserQuizResponse>(entity =>
        {
            entity.HasIndex(r => new { r.UserId, r.AnsweredAt });
            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.Question)
                .WithMany()
                .HasForeignKey(r => r.QuizQuestionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Answer)
                .WithMany()
                .HasForeignKey(r => r.QuizAnswerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Snapshot)
                .WithMany()
                .HasForeignKey(r => r.UserProfileSnapshotId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureCycleDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CycleLog>(entity =>
        {
            entity.HasIndex(c => new { c.UserId, c.StartDate });
            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("CK_CycleLog_CycleLength", "CycleLength >= 0"));
        });

        modelBuilder.Entity<SymptomLog>(entity =>
        {
            entity.HasIndex(s => new { s.UserId, s.Date });
            entity.HasIndex(s => new { s.CycleLogId, s.Date });
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.CycleLog)
                .WithMany(c => c.SymptomLogs)
                .HasForeignKey(s => s.CycleLogId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(s => s.Notes).HasMaxLength(1000);
            entity.ToTable(t => t.HasCheckConstraint("CK_SymptomLog_Severity", "Severity >= 1 AND Severity <= 5"));
        });

        modelBuilder.Entity<CyclePhaseLog>(entity =>
        {
            entity.HasIndex(l => new { l.UserId, l.LoggedAt });
            entity.HasIndex(l => new { l.CycleLogId, l.LoggedAt });
            entity.Property(l => l.Phase).HasConversion<int>();
            entity.Property(l => l.Note).HasMaxLength(1000);
            entity.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(l => l.CycleLog)
                .WithMany(c => c.PhaseLogs)
                .HasForeignKey(l => l.CycleLogId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureNutritionDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FoodItem>(entity =>
        {
            entity.HasIndex(f => f.Name);
            entity.HasIndex(f => f.FdcId).IsUnique();
            entity.HasIndex(f => new { f.DataType, f.Name });
            entity.HasIndex(f => f.BrandOwner);
            entity.HasIndex(f => f.BrandName);
            entity.HasIndex(f => f.GtinUpc);
            entity.Property(f => f.Name).IsRequired().HasMaxLength(240);
            entity.Property(f => f.DataType).HasMaxLength(40);
            entity.Property(f => f.BrandOwner).HasMaxLength(240);
            entity.Property(f => f.BrandName).HasMaxLength(240);
            entity.Property(f => f.GtinUpc).HasMaxLength(40);
            entity.Property(f => f.Ingredients).HasMaxLength(4000);
            entity.Property(f => f.ServingSizeUnit).HasMaxLength(40);
            entity.Property(f => f.ServingOptionsJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<FoodItemVersion>(entity =>
        {
            entity.HasIndex(v => new { v.FoodItemId, v.VersionedAt });
            entity.Property(v => v.NutrientJson).IsRequired();
            entity.Property(v => v.ChangeSource).IsRequired().HasMaxLength(40);
            entity.HasOne(v => v.FoodItem)
                .WithMany(f => f.Versions)
                .HasForeignKey(v => v.FoodItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FoodSyncLog>(entity =>
        {
            entity.HasIndex(l => l.StartedAt);
            entity.Property(l => l.Status).IsRequired().HasMaxLength(40);
            entity.Property(l => l.ErrorDetails).HasMaxLength(4000);
        });

        modelBuilder.Entity<MealTemplate>(entity =>
        {
            entity.HasIndex(t => new { t.UserId, t.Name });
            entity.Property(t => t.Name).IsRequired().HasMaxLength(160);
            entity.Property(t => t.Description).HasMaxLength(1000);
            entity.Property(t => t.DietaryTags).HasMaxLength(250);
            entity.Property(t => t.PhaseTags).HasMaxLength(250);
            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MealTemplateEntry>(entity =>
        {
            entity.HasOne(e => e.MealTemplate)
                .WithMany(t => t.Entries)
                .HasForeignKey(e => e.MealTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FoodItem)
                .WithMany()
                .HasForeignKey(e => e.FoodItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_MealTemplateEntry_Grams", "Grams > 0"));
        });

        modelBuilder.Entity<LoggedMeal>(entity =>
        {
            entity.HasIndex(m => new { m.UserId, m.Date });
            entity.HasIndex(m => new { m.UserId, m.LoggedAt });
            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(m => m.MealTemplate)
                .WithMany()
                .HasForeignKey(m => m.MealTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LoggedMealEntry>(entity =>
        {
            entity.HasOne(e => e.LoggedMeal)
                .WithMany(m => m.Entries)
                .HasForeignKey(e => e.LoggedMealId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FoodItem)
                .WithMany(f => f.LoggedMealEntries)
                .HasForeignKey(e => e.FoodItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_LoggedMealEntry_Grams", "Grams > 0"));
        });
    }

    private static void ConfigureWorkoutDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Exercise>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Code);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(160);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.ImageUrl).HasMaxLength(1000);
            entity.Property(e => e.VideoUrl).HasMaxLength(1000);
            entity.Property(e => e.TechniqueNotes).HasMaxLength(2000);
            entity.Property(e => e.SecondaryMuscles).HasMaxLength(500);
            entity.Property(e => e.JointAreas).HasMaxLength(500);
        });

        modelBuilder.Entity<WorkoutPlan>(entity =>
        {
            entity.HasIndex(p => new { p.UserId, p.IsActive });
            entity.Property(p => p.Name).IsRequired().HasMaxLength(160);
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkoutDay>(entity =>
        {
            entity.HasIndex(d => new { d.WorkoutPlanId, d.DayOfWeek }).IsUnique();
            entity.Property(d => d.WorkoutType).HasConversion<int>();
            entity.Property(d => d.Name).IsRequired().HasMaxLength(160);
            entity.HasOne(d => d.WorkoutPlan)
                .WithMany(p => p.WorkoutDays)
                .HasForeignKey(d => d.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("CK_WorkoutDay_DayOfWeek", "DayOfWeek >= 0 AND DayOfWeek <= 6"));
        });

        modelBuilder.Entity<WorkoutDayExercise>(entity =>
        {
            entity.HasIndex(e => new { e.WorkoutDayId, e.ExerciseId });
            entity.HasOne(e => e.WorkoutDay)
                .WithMany(d => d.WorkoutDayExercises)
                .HasForeignKey(e => e.WorkoutDayId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Exercise)
                .WithMany(e => e.WorkoutExercises)
                .HasForeignKey(e => e.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_WorkoutDayExercise_Sets", "Sets >= 0");
                t.HasCheckConstraint("CK_WorkoutDayExercise_Reps", "Reps >= 0");
                t.HasCheckConstraint("CK_WorkoutDayExercise_Duration", "DurationSeconds IS NULL OR DurationSeconds > 0");
            });
        });

        modelBuilder.Entity<WorkoutLog>(entity =>
        {
            entity.HasIndex(l => new { l.UserId, l.Date });
            entity.Property(l => l.Notes).HasMaxLength(1000);
            entity.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(l => l.WorkoutDay)
                .WithMany()
                .HasForeignKey(l => l.WorkoutDayId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_WorkoutLog_CompletionPercent", "CompletionPercent >= 0 AND CompletionPercent <= 100"));
        });

        modelBuilder.Entity<WorkoutExerciseLog>(entity =>
        {
            entity.HasIndex(l => new { l.ExerciseId, l.CreatedAt });
            entity.HasIndex(l => new { l.WorkoutLogId, l.WorkoutDayExerciseId }).IsUnique();
            entity.HasOne(l => l.WorkoutLog)
                .WithMany(l => l.ExerciseLogs)
                .HasForeignKey(l => l.WorkoutLogId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(l => l.WorkoutDayExercise)
                .WithMany()
                .HasForeignKey(l => l.WorkoutDayExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(l => l.Exercise)
                .WithMany()
                .HasForeignKey(l => l.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_WorkoutExerciseLog_Sets", "CompletedSets >= 0");
                t.HasCheckConstraint("CK_WorkoutExerciseLog_Reps", "CompletedReps >= 0");
                t.HasCheckConstraint("CK_WorkoutExerciseLog_Weight", "WeightKg IS NULL OR WeightKg >= 0");
                t.HasCheckConstraint("CK_WorkoutExerciseLog_Duration", "CompletedDurationSeconds IS NULL OR CompletedDurationSeconds >= 0");
                t.HasCheckConstraint("CK_WorkoutExerciseLog_Distance", "DistanceKm IS NULL OR DistanceKm >= 0");
                t.HasCheckConstraint("CK_WorkoutExerciseLog_HeartRate", "AverageHeartRateBpm IS NULL OR AverageHeartRateBpm >= 0");
                t.HasCheckConstraint("CK_WorkoutExerciseLog_Pace", "PaceSecondsPerKm IS NULL OR PaceSecondsPerKm >= 0");
            });
        });
    }

}
