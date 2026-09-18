using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase : DbContext
{
    private static readonly SemaphoreSlim SeedGate = new(1, 1);

    public AppDatabase(DbContextOptions<AppDatabase> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserProfileSnapshot> UserProfileSnapshots => Set<UserProfileSnapshot>();

    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    public DbSet<UserQuizResponse> UserQuizResponses => Set<UserQuizResponse>();

    public DbSet<CycleLog> CycleLogs => Set<CycleLog>();
    public DbSet<CyclePhaseLog> CyclePhaseLogs => Set<CyclePhaseLog>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();

    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<FoodItemVersion> FoodItemVersions => Set<FoodItemVersion>();
    public DbSet<FoodSyncLog> FoodSyncLogs => Set<FoodSyncLog>();
    public DbSet<MealTemplate> MealTemplates => Set<MealTemplate>();
    public DbSet<MealTemplateEntry> MealTemplateEntries => Set<MealTemplateEntry>();
    public DbSet<LoggedMeal> LoggedMeals => Set<LoggedMeal>();
    public DbSet<LoggedMealEntry> LoggedMealEntries => Set<LoggedMealEntry>();

    public DbSet<DailyReadinessLog> DailyReadinessLogs => Set<DailyReadinessLog>();
    public DbSet<WorkoutInjuryLog> WorkoutInjuryLogs => Set<WorkoutInjuryLog>();

    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutDay> WorkoutDays => Set<WorkoutDay>();
    public DbSet<WorkoutDayExercise> WorkoutDayExercises => Set<WorkoutDayExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<WorkoutExerciseLog> WorkoutExerciseLogs => Set<WorkoutExerciseLog>();

    public DbSet<WorkoutMuscleGroup> WorkoutMuscleGroups => Set<WorkoutMuscleGroup>();
    public DbSet<GoalTierWeight> GoalTierWeights => Set<GoalTierWeight>();
    public DbSet<WorkoutExerciseDefinition> WorkoutExerciseDefinitions => Set<WorkoutExerciseDefinition>();
    public DbSet<ExerciseMuscleContribution> ExerciseMuscleContributions => Set<ExerciseMuscleContribution>();
    public DbSet<SessionArchetype> SessionArchetypes => Set<SessionArchetype>();
    public DbSet<SlotTemplate> SlotTemplates => Set<SlotTemplate>();
    public DbSet<WeekTemplate> WeekTemplates => Set<WeekTemplate>();
    public DbSet<VolumeBudgetRow> VolumeBudgetRows => Set<VolumeBudgetRow>();
    public DbSet<WorkoutPlanningConfigEntry> WorkoutPlanningConfigEntries => Set<WorkoutPlanningConfigEntry>();
    public DbSet<UserExercisePreference> UserExercisePreferences => Set<UserExercisePreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUserDomain(modelBuilder);
        ConfigureQuizDomain(modelBuilder);
        ConfigureCycleDomain(modelBuilder);
        ConfigureNutritionDomain(modelBuilder);
        ConfigureWorkoutDomain(modelBuilder);
        ConfigureWorkoutPlanningDomain(modelBuilder);
        ConfigureWorkoutPlanningReferenceDomain(modelBuilder);
    }

    public async Task InitializeAsync()
    {
        await Database.EnsureCreatedAsync();
        await SeedReferenceDataAsync();
    }

    public async Task InitializeStartupAsync()
    {
        await Database.EnsureCreatedAsync();
        await EnsureMissingTablesAsync();
        await MigrateInjuryLogSchemaAsync();
        await MigrateExercisePreferencesAsync();
        await BackfillEngineCodesAsync();
        await SeedQuizQuestionsAsync();
    }

    private async Task EnsureMissingTablesAsync()
    {
        var conn = Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await conn.OpenAsync();
        try
        {
            // Collect existing table names
            var existing = new HashSet<string>(StringComparer.Ordinal);
            await using (var list = conn.CreateCommand())
            {
                list.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                await using var reader = await list.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    existing.Add(reader.GetString(0));
            }

            // Tables that EnsureCreatedAsync may not have created on older databases
            var migrations = new (string Table, string CreateSql)[]
            {
                ("FoodSyncLogs", """
                    CREATE TABLE "FoodSyncLogs" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_FoodSyncLogs" PRIMARY KEY AUTOINCREMENT,
                        "StartedAt" TEXT NOT NULL,
                        "CompletedAt" TEXT,
                        "ItemsUpserted" INTEGER NOT NULL DEFAULT 0,
                        "ItemsFailed" INTEGER NOT NULL DEFAULT 0,
                        "Status" TEXT NOT NULL,
                        "ErrorDetails" TEXT
                    );
                    CREATE INDEX "IX_FoodSyncLogs_StartedAt" ON "FoodSyncLogs" ("StartedAt");
                    """),

                ("FoodItemVersions", """
                    CREATE TABLE "FoodItemVersions" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_FoodItemVersions" PRIMARY KEY AUTOINCREMENT,
                        "FoodItemId" INTEGER NOT NULL,
                        "NutrientJson" TEXT NOT NULL,
                        "VersionedAt" TEXT NOT NULL,
                        "ChangeSource" TEXT NOT NULL,
                        CONSTRAINT "FK_FoodItemVersions_FoodItems_FoodItemId"
                            FOREIGN KEY ("FoodItemId") REFERENCES "FoodItems" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX "IX_FoodItemVersions_FoodItemId_VersionedAt"
                        ON "FoodItemVersions" ("FoodItemId", "VersionedAt");
                    """),

                ("LoggedMealEntries", """
                    CREATE TABLE "LoggedMealEntries" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_LoggedMealEntries" PRIMARY KEY AUTOINCREMENT,
                        "LoggedMealId" INTEGER NOT NULL,
                        "FoodItemId" INTEGER NOT NULL,
                        "Grams" REAL NOT NULL,
                        CONSTRAINT "FK_LoggedMealEntries_LoggedMeals_LoggedMealId"
                            FOREIGN KEY ("LoggedMealId") REFERENCES "LoggedMeals" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_LoggedMealEntries_FoodItems_FoodItemId"
                            FOREIGN KEY ("FoodItemId") REFERENCES "FoodItems" ("Id") ON DELETE RESTRICT
                    );
                    """),

                ("MealTemplates", """
                    CREATE TABLE "MealTemplates" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_MealTemplates" PRIMARY KEY AUTOINCREMENT,
                        "UserId" INTEGER,
                        "Name" TEXT NOT NULL,
                        "Description" TEXT,
                        "MealType" INTEGER NOT NULL,
                        "DietaryTags" TEXT NOT NULL DEFAULT '',
                        "PhaseTags" TEXT NOT NULL DEFAULT '',
                        "SortOrder" INTEGER NOT NULL DEFAULT 0,
                        "IsSystem" INTEGER NOT NULL DEFAULT 0,
                        "CreatedAt" TEXT NOT NULL,
                        CONSTRAINT "FK_MealTemplates_Users_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX "IX_MealTemplates_UserId_Name" ON "MealTemplates" ("UserId", "Name");
                    """),

                ("MealTemplateEntries", """
                    CREATE TABLE "MealTemplateEntries" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_MealTemplateEntries" PRIMARY KEY AUTOINCREMENT,
                        "MealTemplateId" INTEGER NOT NULL,
                        "FoodItemId" INTEGER NOT NULL,
                        "Grams" REAL NOT NULL,
                        CONSTRAINT "FK_MealTemplateEntries_MealTemplates_MealTemplateId"
                            FOREIGN KEY ("MealTemplateId") REFERENCES "MealTemplates" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_MealTemplateEntries_FoodItems_FoodItemId"
                            FOREIGN KEY ("FoodItemId") REFERENCES "FoodItems" ("Id") ON DELETE RESTRICT
                    );
                    """),

                ("SymptomLogs", """
                    CREATE TABLE "SymptomLogs" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_SymptomLogs" PRIMARY KEY AUTOINCREMENT,
                        "UserId" INTEGER NOT NULL,
                        "CycleLogId" INTEGER NOT NULL,
                        "Date" TEXT NOT NULL,
                        "SymptomType" INTEGER NOT NULL,
                        "Severity" INTEGER NOT NULL,
                        "Notes" TEXT,
                        "CreatedAt" TEXT NOT NULL,
                        CONSTRAINT "FK_SymptomLogs_CycleLogs_CycleLogId"
                            FOREIGN KEY ("CycleLogId") REFERENCES "CycleLogs" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX "IX_SymptomLogs_UserId_Date" ON "SymptomLogs" ("UserId", "Date");
                    CREATE INDEX "IX_SymptomLogs_CycleLogId_Date" ON "SymptomLogs" ("CycleLogId", "Date");
                    """),

                ("EngineConfig", """
                    CREATE TABLE "EngineConfig" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_EngineConfig" PRIMARY KEY AUTOINCREMENT,
                        "Section" TEXT NOT NULL,
                        "Key" TEXT NOT NULL,
                        "Value" TEXT NOT NULL
                    );
                    CREATE UNIQUE INDEX "IX_EngineConfig_Section_Key" ON "EngineConfig" ("Section", "Key");
                    """),
            };

            var missing = migrations.Where(m => !existing.Contains(m.Table)).ToArray();
            if (missing.Length == 0)
                return;

            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                foreach (var (table, sql) in missing)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx as System.Data.Common.DbTransaction;
                    cmd.CommandText = sql;
                    await cmd.ExecuteNonQueryAsync();
                }
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        finally
        {
            if (!wasOpen)
                await conn.CloseAsync();
        }
    }

    private async Task MigrateInjuryLogSchemaAsync()
    {
        var conn = Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA table_info('EngineInjuryLogs')";
            var hasSiteColumn = false;
            var hasSiteFlagColumn = false;
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (name == "Site") hasSiteColumn = true;
                    if (name == "SiteFlag") hasSiteFlagColumn = true;
                }
            }

            if (hasSiteColumn && !hasSiteFlagColumn)
            {
                await using var tx = await conn.BeginTransactionAsync();
                try
                {
                    await using var recreate = conn.CreateCommand();
                    recreate.Transaction = tx as System.Data.Common.DbTransaction;
                    recreate.CommandText = """
                        CREATE TABLE "EngineInjuryLogs_new" (
                            "Id" INTEGER NOT NULL CONSTRAINT "PK_EngineInjuryLogs" PRIMARY KEY AUTOINCREMENT,
                            "UserId" INTEGER NOT NULL,
                            "SiteFlag" INTEGER NOT NULL,
                            "Status" TEXT NOT NULL,
                            "Since" TEXT NOT NULL,
                            "Pain" INTEGER NOT NULL,
                            "Date" TEXT NOT NULL,
                            "CreatedAt" TEXT NOT NULL
                        )
                        """;
                    await recreate.ExecuteNonQueryAsync();

                    await using var copy = conn.CreateCommand();
                    copy.Transaction = tx as System.Data.Common.DbTransaction;
                    copy.CommandText = """
                        INSERT INTO "EngineInjuryLogs_new"
                            ("Id", "UserId", "SiteFlag", "Status", "Since", "Pain", "Date", "CreatedAt")
                        SELECT "Id", "UserId",
                            CASE lower(trim("Site"))
                                WHEN 'metatarsal' THEN 1
                                WHEN 'knee' THEN 2
                                WHEN 'ankle' THEN 4
                                WHEN 'shoulder' THEN 8
                                WHEN 'lowback' THEN 16
                                WHEN 'wrist' THEN 32
                                WHEN 'hip' THEN 64
                                WHEN 'neck' THEN 128
                                ELSE 0
                            END,
                            "Status", "Since", "Pain", "Date", "CreatedAt"
                        FROM "EngineInjuryLogs"
                        """;
                    await copy.ExecuteNonQueryAsync();

                    await using var drop = conn.CreateCommand();
                    drop.Transaction = tx as System.Data.Common.DbTransaction;
                    drop.CommandText = "DROP TABLE \"EngineInjuryLogs\"";
                    await drop.ExecuteNonQueryAsync();

                    await using var rename = conn.CreateCommand();
                    rename.Transaction = tx as System.Data.Common.DbTransaction;
                    rename.CommandText = "ALTER TABLE \"EngineInjuryLogs_new\" RENAME TO \"EngineInjuryLogs\"";
                    await rename.ExecuteNonQueryAsync();

                    await using var idx = conn.CreateCommand();
                    idx.Transaction = tx as System.Data.Common.DbTransaction;
                    idx.CommandText = """
                        CREATE INDEX "IX_EngineInjuryLogs_UserId_Date"
                        ON "EngineInjuryLogs" ("UserId", "Date")
                        """;
                    await idx.ExecuteNonQueryAsync();

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
        }
        finally
        {
            if (!wasOpen)
                await conn.CloseAsync();
        }
    }

    private async Task MigrateExercisePreferencesAsync()
    {
        var conn = Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await conn.OpenAsync();
        try
        {
            await using var check = conn.CreateCommand();
            check.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='EngineExercisePreferences'";
            var exists = Convert.ToInt64(await check.ExecuteScalarAsync()) > 0;
            if (!exists)
            {
                await using var tx = await conn.BeginTransactionAsync();
                try
                {
                    await using var create = conn.CreateCommand();
                    create.Transaction = tx as System.Data.Common.DbTransaction;
                    create.CommandText = """
                        CREATE TABLE "EngineExercisePreferences" (
                            "Id" INTEGER NOT NULL CONSTRAINT "PK_EngineExercisePreferences" PRIMARY KEY AUTOINCREMENT,
                            "UserId" INTEGER NOT NULL,
                            "OriginalExerciseId" INTEGER NOT NULL,
                            "PreferredExerciseId" INTEGER NOT NULL,
                            "CreatedAt" TEXT NOT NULL
                        )
                        """;
                    await create.ExecuteNonQueryAsync();

                    await using var idx = conn.CreateCommand();
                    idx.Transaction = tx as System.Data.Common.DbTransaction;
                    idx.CommandText = """
                        CREATE UNIQUE INDEX "IX_EngineExercisePreferences_UserId_OriginalExerciseId"
                        ON "EngineExercisePreferences" ("UserId", "OriginalExerciseId")
                        """;
                    await idx.ExecuteNonQueryAsync();

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
        }
        finally
        {
            if (!wasOpen)
                await conn.CloseAsync();
        }
    }

    private async Task BackfillEngineCodesAsync()
    {
        if (!await WorkoutExerciseDefinitions.AnyAsync())
            return;

        var planningExercises = await WorkoutExerciseDefinitions
            .AsNoTracking()
            .Select(e => new { e.Id, e.Name })
            .ToListAsync();

        var catalogExercises = await Exercises.ToListAsync();
        var catalogByName = catalogExercises
            .GroupBy(e => string.Concat(e.Name.Where(char.IsLetterOrDigit)).ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var pe in planningExercises)
        {
            var engineCode = $"ENGINE_{pe.Id}";
            if (catalogExercises.Any(e => e.Code == engineCode))
                continue;

            var normalized = string.Concat(pe.Name.Where(char.IsLetterOrDigit)).ToUpperInvariant();
            if (catalogByName.TryGetValue(normalized, out var match)
                && !match.Code.StartsWith("ENGINE_", StringComparison.Ordinal))
            {
                match.Code = engineCode;
                changed = true;
            }
        }

        if (changed)
            await SaveChangesAsync();
    }

    public async Task SeedReferenceDataAsync()
    {
        await SeedQuizQuestionsAsync();
        await SeedDeferredReferenceDataAsync();
    }

    public async Task SeedDeferredReferenceDataAsync()
    {
        await SeedGate.WaitAsync();
        try
        {
            if (await WorkoutMuscleGroups.AnyAsync())
                return;

            await SeedStarterFoodItemsAsync();
            await SeedStarterMealTemplatesAsync();
            await SeedWorkoutPlanningDataAsync_Unguarded();
        }
        finally
        {
            SeedGate.Release();
        }
    }

    public async Task SeedWorkoutPlanningDataAsync()
    {
        await SeedGate.WaitAsync();
        try
        {
            await SeedWorkoutPlanningDataAsync_Unguarded();
        }
        finally
        {
            SeedGate.Release();
        }
    }

    private async Task SeedWorkoutPlanningDataAsync_Unguarded()
    {
        await SeedWorkoutPlanningReferenceDataAsync();
        await SeedStarterExercisesAsync();
        await MirrorPlanningExercisesToWorkoutCatalogAsync();
    }

    public async Task ResetAndSeedDebugDatabaseAsync()
    {
#if DEBUG
        ChangeTracker.Clear();
        await Database.EnsureDeletedAsync();
        await Database.EnsureCreatedAsync();
        await SeedReferenceDataAsync();
        ChangeTracker.Clear();
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
                Require((int)profile.TrainingExperienceLevel is >= 0 and <= 3,
                    "Training experience is outside the supported range.");
                Require((int)profile.CycleTrackingMode is >= 0 and <= 3,
                    "Cycle tracking mode is outside the supported range.");
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
                Require(workoutDayExercise.DurationSeconds is null or > 0,
                    "Workout exercise duration must be greater than zero.");
                break;

            case WorkoutLog workoutLog:
                Require(workoutLog.CompletionPercent is >= 0 and <= 100,
                    "Workout completion must be between 0 and 100 percent.");
                break;

            case DailyReadinessLog readiness:
                Require(readiness.Energy is >= 1 and <= 5, "Energy must be between 1 and 5.");
                Require(readiness.Pain is >= 0 and <= 3, "Pain must be between 0 and 3.");
                Require(readiness.ReadinessScore is >= 0 and <= 100, "Readiness score must be between 0 and 100.");
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
            entity.Property(p => p.EquipmentLevel).HasMaxLength(50);
            entity.Property(p => p.PhaseBaselinesJson).HasMaxLength(500);
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
            entity.HasIndex(d => new { d.WorkoutPlanId, d.DayOfWeek });
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

    private static void ConfigureWorkoutPlanningDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyReadinessLog>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();
            entity.Property(e => e.Phase).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<WorkoutInjuryLog>(entity =>
        {
            entity.ToTable("EngineInjuryLogs");
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.Property(e => e.SiteFlag).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<UserExercisePreference>(entity =>
        {
            entity.ToTable("EngineExercisePreferences");
            entity.HasIndex(e => new { e.UserId, e.OriginalExerciseId }).IsUnique();
        });
    }

    private static void ConfigureWorkoutPlanningReferenceDomain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkoutMuscleGroup>(entity =>
        {
            entity.ToTable("EngineMuscleGroups");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(40);
        });

        modelBuilder.Entity<GoalTierWeight>(entity =>
        {
            entity.HasIndex(e => new { e.Goal, e.MuscleGroupId }).IsUnique();
            entity.Property(e => e.Goal).HasConversion<int>();
            entity.Property(e => e.Tier).HasConversion<int>();
            entity.HasOne(e => e.MuscleGroup)
                .WithMany()
                .HasForeignKey(e => e.MuscleGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkoutExerciseDefinition>(entity =>
        {
            entity.ToTable("EngineExercises");
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Pattern).HasConversion<int>();
            entity.Property(e => e.Required).HasConversion<int>();
            entity.Property(e => e.Contraindications).HasConversion<int>();
            entity.Property(e => e.PreferredFor).HasConversion<int>();
        });

        modelBuilder.Entity<ExerciseMuscleContribution>(entity =>
        {
            entity.HasIndex(e => new { e.ExerciseId, e.MuscleGroupId }).IsUnique();
            entity.HasOne(e => e.Exercise)
                .WithMany(ex => ex.MuscleContributions)
                .HasForeignKey(e => e.ExerciseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MuscleGroup)
                .WithMany()
                .HasForeignKey(e => e.MuscleGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SessionArchetype>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(60);
        });

        modelBuilder.Entity<SlotTemplate>(entity =>
        {
            entity.HasIndex(e => new { e.ArchetypeId, e.Order }).IsUnique();
            entity.Property(e => e.Block).HasConversion<int>();
            entity.Property(e => e.AllowedPatternsJson).IsRequired().HasMaxLength(500);
            entity.HasOne(e => e.Archetype)
                .WithMany(a => a.SlotTemplates)
                .HasForeignKey(e => e.ArchetypeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PrimaryMuscle)
                .WithMany()
                .HasForeignKey(e => e.PrimaryMuscleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WeekTemplate>(entity =>
        {
            entity.HasIndex(e => e.DaysPerWeek).IsUnique();
            entity.Property(e => e.ArchetypeSequenceJson).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<VolumeBudgetRow>(entity =>
        {
            entity.HasIndex(e => new { e.DaysPerWeek, e.MuscleGroupId }).IsUnique();
            entity.HasOne(e => e.MuscleGroup)
                .WithMany()
                .HasForeignKey(e => e.MuscleGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkoutPlanningConfigEntry>(entity =>
        {
            entity.ToTable("EngineConfig");
            entity.HasIndex(e => new { e.Section, e.Key }).IsUnique();
            entity.Property(e => e.Section).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(60);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
        });
    }

}
