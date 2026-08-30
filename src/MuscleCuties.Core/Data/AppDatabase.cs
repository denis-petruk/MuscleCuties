using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Diagnostics;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;

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
        AppDebugLog.Write("Database", "InitializeAsync start.");
        var wasCreated = await Database.EnsureCreatedAsync();
        AppDebugLog.Write("Database", $"EnsureCreated completed. wasCreated={wasCreated}.");

        await SeedReferenceDataAsync();
        AppDebugLog.Write("Database", "InitializeAsync finished.");
    }

    public async Task SeedReferenceDataAsync()
    {
        AppDebugLog.Write("Database", "SeedReferenceData start.");
        await TrySeedStepAsync(SeedQuizQuestionsAsync, nameof(SeedQuizQuestionsAsync));
        await TrySeedStepAsync(SeedStarterFoodItemsAsync, nameof(SeedStarterFoodItemsAsync));
        await TrySeedStepAsync(SeedSystemMealTemplatesAsync, nameof(SeedSystemMealTemplatesAsync));
        await TrySeedStepAsync(SeedStarterExercisesAsync, nameof(SeedStarterExercisesAsync));
        AppDebugLog.Write("Database", "SeedReferenceData finished.");
    }

    private static async Task TrySeedStepAsync(Func<Task> seedStep, string stepName)
    {
        try
        {
            AppDebugLog.Write("Database", $"Seed step start: {stepName}.");
            await seedStep();
            AppDebugLog.Write("Database", $"Seed step complete: {stepName}.");
        }
        catch (Exception ex)
        {
            AppDebugLog.Error("Database", ex, $"Seed step failed: {stepName}");
        }
    }

    public async Task ResetAndSeedDebugDatabaseAsync()
    {
#if DEBUG
        ChangeTracker.Clear();
        AppDebugLog.Write("Database", "ResetAndSeedDebugDatabase start.");
        await Database.EnsureDeletedAsync();
        await Database.EnsureCreatedAsync();
        await SeedReferenceDataAsync();
        ChangeTracker.Clear();
        AppDebugLog.Write("Database", "ResetAndSeedDebugDatabase finished.");
#else
        throw new InvalidOperationException("Debug database reset is only available in DEBUG builds.");
#endif
    }

    public override int SaveChanges()
    {
        ValidatePendingChanges();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidatePendingChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidatePendingChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidatePendingChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidatePendingChanges()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            ValidateEntity(entry.Entity);
        }
    }

    private static void ValidateEntity(object entity)
    {
        switch (entity)
        {
            case UserProfile profile:
                Require(profile.Height >= 0, "Profile height cannot be negative.");
                Require(profile.Weight >= 0, "Profile weight cannot be negative.");
                Require((int)profile.TrainingExperienceLevel is >= 0 and <= 3, "Training experience is outside the supported range.");
                Require((int)profile.CycleTrackingMode is >= 0 and <= 3, "Cycle tracking mode is outside the supported range.");
                Require(profile.WorkoutDaysPerWeek is >= 0 and <= 7, "Workout days per week must be between 0 and 7.");
                Require(profile.CycleLength is >= 0 and <= 60, "Cycle length must be between 0 and 60 days.");
                break;

            case CycleLog cycleLog:
                Require(cycleLog.CycleLength >= 0, "Cycle length cannot be negative.");
                break;

            case SymptomLog symptomLog:
                Require(symptomLog.Severity is >= 1 and <= 5, "Symptom severity must be between 1 and 5.");
                break;

            case MealTemplateEntry mealTemplateEntry:
                Require(mealTemplateEntry.Grams > 0, "Meal template ingredient grams must be greater than zero.");
                break;

            case LoggedMealEntry loggedMealEntry:
                Require(loggedMealEntry.Grams > 0, "Logged meal ingredient grams must be greater than zero.");
                break;

            case WorkoutDay workoutDay:
                Require(workoutDay.DayOfWeek is >= 0 and <= 6, "Workout day must be between 0 and 6.");
                break;

            case WorkoutDayExercise workoutDayExercise:
                Require(workoutDayExercise.Sets >= 0, "Workout exercise sets cannot be negative.");
                Require(workoutDayExercise.Reps >= 0, "Workout exercise reps cannot be negative.");
                Require(workoutDayExercise.DurationSeconds is null or > 0, "Workout exercise duration must be greater than zero.");
                break;

            case WorkoutLog workoutLog:
                Require(workoutLog.CompletionPercent is >= 0 and <= 100, "Workout completion must be between 0 and 100 percent.");
                break;

            case WorkoutExerciseLog exerciseLog:
                Require(exerciseLog.CompletedSets >= 0, "Completed sets cannot be negative.");
                Require(exerciseLog.CompletedReps >= 0, "Completed reps cannot be negative.");
                Require(exerciseLog.WeightKg is null or >= 0, "Workout weight cannot be negative.");
                Require(exerciseLog.CompletedDurationSeconds is null or >= 0, "Completed duration cannot be negative.");
                Require(exerciseLog.DistanceKm is null or >= 0, "Workout distance cannot be negative.");
                Require(exerciseLog.AverageHeartRateBpm is null or >= 0, "Average heart rate cannot be negative.");
                Require(exerciseLog.PaceSecondsPerKm is null or >= 0, "Workout pace cannot be negative.");
                Require(exerciseLog.PowerWatts is null or >= 0, "Workout power cannot be negative.");
                Require(exerciseLog.CadenceRpm is null or >= 0, "Workout cadence cannot be negative.");
                Require(exerciseLog.EffortRating is null or >= 1 and <= 10, "Effort rating must be between 1 and 10.");
                break;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ConfigureUserDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.AppleUserId).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(320);
            entity.Property(u => u.AppleUserId).HasMaxLength(255);
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
            entity.Property(p => p.ProfileImagePath).HasMaxLength(1024);
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
        });
    }
}
