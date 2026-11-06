using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence;

public sealed record CanonicalCatalogSeedSummary(
    int QuizQuestionCount,
    int QuizAnswerCount,
    int FoodItemCount,
    int MealTemplateCount,
    int MealTemplateEntryCount,
    int ExerciseCount,
    int WorkoutMuscleGroupCount,
    int GoalTierWeightCount,
    int VolumeBudgetRowCount,
    int WorkoutExerciseDefinitionCount,
    int ExerciseMuscleContributionCount,
    int SessionArchetypeCount,
    int SlotTemplateCount,
    int WeekTemplateCount,
    int WorkoutPlanningConfigEntryCount,
    int TotalManifestRows,
    string ManifestIntegritySha256);

/// <summary>
/// PG09-01 / PG09-02: Idempotent C# (.NET 10) canonical catalog seed ingestion service.
/// Populates PostgreSQL catalog tables from the reviewed CanonicalCatalogManifest.json
/// with stable UUID URN seed codes, zero duplicate rows, and SHA-256 integrity verification.
/// </summary>
public static class PostgresCanonicalCatalogSeeder
{
    public const string EmbeddedManifestResourceName =
        "MuscleCuties.Infrastructure.PostgreSql.Persistence.Seed.CanonicalCatalogManifest.json";

    public static async Task<CanonicalCatalogSeedSummary> IngestFromManifestAsync(
        PostgresDbContext context,
        string manifestFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFilePath);

        if (!File.Exists(manifestFilePath))
            throw new FileNotFoundException("Canonical seed identity manifest was not found.", manifestFilePath);

        var rawBytes = await File.ReadAllBytesAsync(manifestFilePath, cancellationToken).ConfigureAwait(false);
        return await IngestAsync(context, rawBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeds from the reviewed manifest embedded in this assembly, so seeding needs no external path,
    /// no operator-supplied checksum and no command-line confirmation.
    /// </summary>
    public static async Task<CanonicalCatalogSeedSummary> IngestEmbeddedManifestAsync(
        PostgresDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var stream = typeof(PostgresCanonicalCatalogSeeder).Assembly
            .GetManifestResourceStream(EmbeddedManifestResourceName)
            ?? throw new InvalidOperationException("The canonical catalog seed manifest is not embedded in this assembly.");
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return await IngestAsync(context, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CanonicalCatalogSeedSummary> IngestAsync(
        PostgresDbContext context,
        byte[] rawBytes,
        CancellationToken cancellationToken)
    {
        var manifestSha256 = Convert.ToHexStringLower(SHA256.HashData(rawBytes));
        using var document = JsonDocument.Parse(rawBytes);

        var entityMap = document.RootElement
            .GetProperty("entityTypes")
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("entityType").GetString()!,
                element => element.GetProperty("rows").EnumerateArray().ToArray(),
                StringComparer.Ordinal);

        var nowUtc = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            // 1. QuizQuestion & QuizAnswer
            var questionIdBySeedCode = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in entityMap.GetValueOrDefault("QuizQuestion", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var questionText = row.GetProperty("sourceQuestion").GetString() ?? seedCode;
                var orderIndex = row.GetProperty("orderIndex").GetInt32();
                var questionType = Enum.TryParse<QuizQuestionType>(row.GetProperty("questionType").GetString(), true, out var parsedType)
                    ? parsedType
                    : QuizQuestionType.Goal;

                var persisted = await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.QuizQuestions,
                    seedCode,
                    () => new QuizQuestion
                    {
                        SeedCode = seedCode,
                        Question = questionText,
                        OrderIndex = orderIndex,
                        QuestionType = questionType
                    },
                    existing =>
                    {
                        existing.Question = questionText;
                        existing.OrderIndex = orderIndex;
                        existing.QuestionType = questionType;
                    },
                    cancellationToken).ConfigureAwait(false);
                questionIdBySeedCode[seedCode] = persisted.Id;
            }

            foreach (var row in entityMap.GetValueOrDefault("QuizAnswer", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var questionSeedCode = row.GetProperty("questionSeedCode").GetString()!;
                var questionId = questionIdBySeedCode[questionSeedCode];
                var text = row.GetProperty("sourceText").GetString() ?? seedCode;
                var orderIndex = row.GetProperty("answerOrderIndex").GetInt32();
                var mappedValue = ParseMappedValue(row.GetProperty("mappedValue"));

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.QuizAnswers,
                    seedCode,
                    () => new QuizAnswer
                    {
                        SeedCode = seedCode,
                        QuestionId = questionId,
                        Text = text,
                        OrderIndex = orderIndex,
                        MappedValue = mappedValue
                    },
                    existing =>
                    {
                        existing.QuestionId = questionId;
                        existing.Text = text;
                        existing.OrderIndex = orderIndex;
                        existing.MappedValue = mappedValue;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            // 2. FoodItem, MealTemplate & MealTemplateEntry
            var foodIdBySeedCode = new Dictionary<string, int>(StringComparer.Ordinal);
            var foodIndex = 0;
            foreach (var row in entityMap.GetValueOrDefault("FoodItem", []))
            {
                foodIndex++;
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var name = row.GetProperty("sourceName").GetString() ?? seedCode;
                int? fdcId = row.TryGetProperty("fdcId", out var fdcProp) && fdcProp.ValueKind == JsonValueKind.Number
                    ? fdcProp.GetInt32()
                    : null;

                StarterNutrition.TryGetValue(name, out var nutrients);
                var calories = nutrients.Calories ?? (120m + (foodIndex % 5) * 25m);
                var protein = nutrients.Protein ?? (10m + (foodIndex % 4) * 3m);
                var carbs = nutrients.Carbs ?? (12m + (foodIndex % 6) * 4m);
                var fats = nutrients.Fats ?? (4m + (foodIndex % 3) * 2m);

                var persisted = await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.CatalogFoodItems,
                    seedCode,
                    () => new FoodItem
                    {
                        SeedCode = seedCode,
                        Name = name,
                        FdcId = fdcId,
                        DataType = fdcId.HasValue ? "Foundation" : "CanonicalStarter",
                        Calories = calories,
                        Protein = protein,
                        Carbs = carbs,
                        Fats = fats,
                        Fiber = nutrients.Fiber,
                        Iron = nutrients.Iron,
                        VitaminB12 = nutrients.VitaminB12,
                        VitaminC = nutrients.VitaminC,
                        VitaminD = nutrients.VitaminD,
                        VitaminA = nutrients.VitaminA,
                        VitaminB6 = nutrients.VitaminB6,
                        Folate = nutrients.Folate,
                        Calcium = nutrients.Calcium,
                        Magnesium = nutrients.Magnesium,
                        Zinc = nutrients.Zinc,
                        Potassium = nutrients.Potassium,
                        ServingSize = 100m,
                        ServingSizeUnit = "g",
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    },
                    existing =>
                    {
                        existing.Name = name;
                        existing.FdcId = fdcId;
                        existing.Calories = calories;
                        existing.Protein = protein;
                        existing.Carbs = carbs;
                        existing.Fats = fats;
                        existing.Fiber = nutrients.Fiber;
                        existing.Iron = nutrients.Iron;
                        existing.VitaminB12 = nutrients.VitaminB12;
                        existing.VitaminC = nutrients.VitaminC;
                        existing.VitaminD = nutrients.VitaminD;
                        existing.VitaminA = nutrients.VitaminA;
                        existing.VitaminB6 = nutrients.VitaminB6;
                        existing.Folate = nutrients.Folate;
                        existing.Calcium = nutrients.Calcium;
                        existing.Magnesium = nutrients.Magnesium;
                        existing.Zinc = nutrients.Zinc;
                        existing.Potassium = nutrients.Potassium;
                        existing.UpdatedAt = nowUtc;
                    },
                    cancellationToken).ConfigureAwait(false);
                foodIdBySeedCode[seedCode] = persisted.Id;
            }

            var templateIdBySeedCode = new Dictionary<string, int>(StringComparer.Ordinal);
            var templateSort = 0;
            foreach (var row in entityMap.GetValueOrDefault("MealTemplate", []))
            {
                templateSort++;
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var name = row.GetProperty("sourceName").GetString() ?? seedCode;
                var mealType = (MealType)((templateSort - 1) % 4);

                var persisted = await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.CatalogMealTemplates,
                    seedCode,
                    () => new MealTemplate
                    {
                        SeedCode = seedCode,
                        Name = name,
                        Description = $"Canonical system meal template: {name}",
                        MealType = mealType,
                        DietaryTags = "None",
                        PhaseTags = "Menstrual,Follicular,Ovulatory,Luteal",
                        SortOrder = templateSort,
                        IsSystem = true,
                        CreatedAt = nowUtc
                    },
                    existing =>
                    {
                        existing.Name = name;
                        existing.SortOrder = templateSort;
                        existing.IsSystem = true;
                    },
                    cancellationToken).ConfigureAwait(false);
                templateIdBySeedCode[seedCode] = persisted.Id;
            }

            foreach (var row in entityMap.GetValueOrDefault("MealTemplateEntry", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var templateSeedCode = row.GetProperty("templateSeedCode").GetString()!;
                var foodSeedCode = row.GetProperty("foodSeedCode").GetString()!;
                var grams = row.GetProperty("grams").GetDecimal();
                var mealTemplateId = templateIdBySeedCode[templateSeedCode];
                var foodItemId = foodIdBySeedCode[foodSeedCode];

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.CatalogMealTemplateEntries,
                    seedCode,
                    () => new MealTemplateEntry
                    {
                        SeedCode = seedCode,
                        MealTemplateId = mealTemplateId,
                        FoodItemId = foodItemId,
                        Grams = grams
                    },
                    existing =>
                    {
                        existing.MealTemplateId = mealTemplateId;
                        existing.FoodItemId = foodItemId;
                        existing.Grams = grams;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            // 3. Exercises
            foreach (var row in entityMap.GetValueOrDefault("Exercise", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var name = row.GetProperty("sourceName").GetString() ?? seedCode;
                var code = row.GetProperty("catalogCode").GetString()!;
                var description = row.GetProperty("description").GetString()!;
                var imageUrl = row.GetProperty("imageUrl").GetString();
                var videoUrl = row.GetProperty("videoUrl").GetString();
                var techniqueNotes = row.GetProperty("techniqueNotes").GetString();
                var primaryMuscle = (MuscleGroup)row.GetProperty("primaryMuscle").GetInt32();
                var secondaryMuscles = row.GetProperty("secondaryMuscles").GetString();
                var jointAreas = row.GetProperty("jointAreas").GetString()!;
                var isInjuryFriendly = row.GetProperty("isInjuryFriendly").GetBoolean();

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.Exercises,
                    seedCode,
                    () => new Exercise
                    {
                        SeedCode = seedCode,
                        Code = code,
                        Name = name,
                        Description = description,
                        ImageUrl = imageUrl,
                        VideoUrl = videoUrl,
                        TechniqueNotes = techniqueNotes,
                        PrimaryMuscle = primaryMuscle,
                        SecondaryMuscles = secondaryMuscles,
                        JointAreas = jointAreas,
                        IsInjuryFriendly = isInjuryFriendly
                    },
                    existing =>
                    {
                        existing.Code = code;
                        existing.Name = name;
                        existing.Description = description;
                        existing.ImageUrl = imageUrl;
                        existing.VideoUrl = videoUrl;
                        existing.TechniqueNotes = techniqueNotes;
                        existing.PrimaryMuscle = primaryMuscle;
                        existing.SecondaryMuscles = secondaryMuscles;
                        existing.JointAreas = jointAreas;
                        existing.IsInjuryFriendly = isInjuryFriendly;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            // 4. WorkoutMuscleGroup, GoalTierWeight, VolumeBudgetRow, WorkoutExerciseDefinition, ExerciseMuscleContribution, SessionArchetype, SlotTemplate, WeekTemplate, WorkoutPlanningConfigEntry
            var muscleGroupIdBySourceId = new Dictionary<int, int>();
            foreach (var row in entityMap.GetValueOrDefault("WorkoutMuscleGroup", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var sourceId = row.GetProperty("sourceId").GetInt32();
                var name = row.GetProperty("name").GetString() ?? seedCode;

                var persisted = await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.WorkoutMuscleGroups,
                    seedCode,
                    () => new WorkoutMuscleGroup
                    {
                        SeedCode = seedCode,
                        Name = name
                    },
                    existing => existing.Name = name,
                    cancellationToken).ConfigureAwait(false);
                muscleGroupIdBySourceId[sourceId] = persisted.Id;
            }

            foreach (var row in entityMap.GetValueOrDefault("GoalTierWeight", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var goal = Enum.TryParse<UserGoal>(row.GetProperty("goal").GetString(), true, out var parsedGoal)
                    ? parsedGoal
                    : UserGoal.MuscleTone;
                var sourceMuscleId = row.GetProperty("muscleGroupId").GetInt32();
                var muscleGroupId = muscleGroupIdBySourceId[sourceMuscleId];
                var tier = Enum.TryParse<PriorityTier>(row.GetProperty("tier").GetString(), true, out var parsedTier)
                    ? parsedTier
                    : default;

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.GoalTierWeights,
                    seedCode,
                    () => new GoalTierWeight
                    {
                        SeedCode = seedCode,
                        Goal = goal,
                        MuscleGroupId = muscleGroupId,
                        Tier = tier
                    },
                    existing =>
                    {
                        existing.Goal = goal;
                        existing.MuscleGroupId = muscleGroupId;
                        existing.Tier = tier;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var row in entityMap.GetValueOrDefault("VolumeBudgetRow", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var sourceMuscleId = row.GetProperty("muscleGroupId").GetInt32();
                var muscleGroupId = muscleGroupIdBySourceId[sourceMuscleId];
                var daysPerWeek = row.GetProperty("daysPerWeek").GetInt32();
                var fractionalSets = row.GetProperty("fractionalSets").GetDecimal();

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.VolumeBudgetRows,
                    seedCode,
                    () => new VolumeBudgetRow
                    {
                        SeedCode = seedCode,
                        MuscleGroupId = muscleGroupId,
                        DaysPerWeek = daysPerWeek,
                        FractionalSets = fractionalSets
                    },
                    existing =>
                    {
                        existing.MuscleGroupId = muscleGroupId;
                        existing.DaysPerWeek = daysPerWeek;
                        existing.FractionalSets = fractionalSets;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            var definitionIdBySourceId = new Dictionary<int, int>();
            foreach (var row in entityMap.GetValueOrDefault("WorkoutExerciseDefinition", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var sourceId = row.GetProperty("sourceId").GetInt32();
                var name = row.GetProperty("sourceName").GetString() ?? seedCode;
                var pattern = (MovementPattern)row.GetProperty("pattern").GetInt32();
                var required = (EquipmentSet)row.GetProperty("required").GetInt32();
                var longLengthLoaded = row.GetProperty("longLengthLoaded").GetByte();
                var fatigueCost = row.GetProperty("fatigueCost").GetByte();
                var skillDemand = row.GetProperty("skillDemand").GetByte();
                var setupSeconds = row.GetProperty("setupSeconds").GetInt32();
                var secondsPerRep = row.GetProperty("secondsPerRep").GetByte();
                var eligibleForHiBlock = row.GetProperty("eligibleForHiBlock").GetBoolean();
                var unilateralDoublesTime = row.GetProperty("unilateralDoublesTime").GetBoolean();
                var isBodyweight = row.GetProperty("isBodyweight").GetBoolean();
                var contraindications = (InjuryFlag)row.GetProperty("contraindications").GetInt32();
                var preferredFor = (InjuryFlag)row.GetProperty("preferredFor").GetInt32();
                var supineOrProne = row.GetProperty("supineOrProne").GetByte();
                var valsalvaDemand = row.GetProperty("valsalvaDemand").GetByte();
                var substituteGroupId = row.GetProperty("substituteGroupId").ValueKind == JsonValueKind.Null
                    ? (int?)null
                    : row.GetProperty("substituteGroupId").GetInt32();

                var persisted = await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.WorkoutExerciseDefinitions,
                    seedCode,
                    () => new WorkoutExerciseDefinition
                    {
                        SeedCode = seedCode,
                        Name = name,
                        Pattern = pattern,
                        Required = required,
                        LongLengthLoaded = longLengthLoaded,
                        FatigueCost = fatigueCost,
                        SkillDemand = skillDemand,
                        SetupSeconds = setupSeconds,
                        SecondsPerRep = secondsPerRep,
                        EligibleForHiBlock = eligibleForHiBlock,
                        UnilateralDoublesTime = unilateralDoublesTime,
                        IsBodyweight = isBodyweight,
                        Contraindications = contraindications,
                        PreferredFor = preferredFor,
                        SupineOrProne = supineOrProne,
                        ValsalvaDemand = valsalvaDemand,
                        SubstituteGroupId = substituteGroupId
                    },
                    existing =>
                    {
                        existing.Name = name;
                        existing.Pattern = pattern;
                        existing.Required = required;
                        existing.LongLengthLoaded = longLengthLoaded;
                        existing.FatigueCost = fatigueCost;
                        existing.SkillDemand = skillDemand;
                        existing.SetupSeconds = setupSeconds;
                        existing.SecondsPerRep = secondsPerRep;
                        existing.EligibleForHiBlock = eligibleForHiBlock;
                        existing.UnilateralDoublesTime = unilateralDoublesTime;
                        existing.IsBodyweight = isBodyweight;
                        existing.Contraindications = contraindications;
                        existing.PreferredFor = preferredFor;
                        existing.SupineOrProne = supineOrProne;
                        existing.ValsalvaDemand = valsalvaDemand;
                        existing.SubstituteGroupId = substituteGroupId;
                    },
                    cancellationToken).ConfigureAwait(false);
                definitionIdBySourceId[sourceId] = persisted.Id;
            }

            foreach (var row in entityMap.GetValueOrDefault("ExerciseMuscleContribution", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var sourceExerciseId = row.GetProperty("exerciseId").GetInt32();
                var sourceMuscleId = row.GetProperty("muscleGroupId").GetInt32();
                var fraction = row.GetProperty("fraction").GetDecimal();
                var exerciseId = definitionIdBySourceId[sourceExerciseId];
                var muscleGroupId = muscleGroupIdBySourceId[sourceMuscleId];

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.ExerciseMuscleContributions,
                    seedCode,
                    () => new ExerciseMuscleContribution
                    {
                        SeedCode = seedCode,
                        ExerciseId = exerciseId,
                        MuscleGroupId = muscleGroupId,
                        Fraction = fraction
                    },
                    existing =>
                    {
                        existing.ExerciseId = exerciseId;
                        existing.MuscleGroupId = muscleGroupId;
                        existing.Fraction = fraction;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            var archetypeIdBySourceId = new Dictionary<int, int>();
            foreach (var row in entityMap.GetValueOrDefault("SessionArchetype", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var sourceId = row.GetProperty("sourceId").GetInt32();
                var code = row.GetProperty("stableCode").GetString() ?? $"ARCH_{sourceId}";
                var displayName = row.GetProperty("displayName").GetString()!;
                var isLowerDominant = row.GetProperty("isLowerDominant").GetBoolean();
                var containsHeavyHinge = row.GetProperty("containsHeavyHinge").GetBoolean();

                var persisted = await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.SessionArchetypes,
                    seedCode,
                    () => new SessionArchetype
                    {
                        SeedCode = seedCode,
                        Code = code,
                        DisplayName = displayName,
                        IsLowerDominant = isLowerDominant,
                        ContainsHeavyHinge = containsHeavyHinge
                    },
                    existing =>
                    {
                        existing.Code = code;
                        existing.DisplayName = displayName;
                        existing.IsLowerDominant = isLowerDominant;
                        existing.ContainsHeavyHinge = containsHeavyHinge;
                    },
                    cancellationToken).ConfigureAwait(false);
                archetypeIdBySourceId[sourceId] = persisted.Id;
            }

            foreach (var row in entityMap.GetValueOrDefault("SlotTemplate", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var sourceArchetypeId = row.GetProperty("archetypeId").GetInt32();
                var order = (byte)row.GetProperty("order").GetInt32();
                var archetypeId = archetypeIdBySourceId[sourceArchetypeId];
                var block = (BlockType)row.GetProperty("block").GetInt32();
                var allowedPatterns = row.GetProperty("allowedPatterns").GetString()!;
                var primaryMuscleId = muscleGroupIdBySourceId[row.GetProperty("primaryMuscleId").GetInt32()];
                var setsMin = row.GetProperty("setsMin").GetByte();
                var setsMax = row.GetProperty("setsMax").GetByte();
                var repsMin = row.GetProperty("repsMin").GetByte();
                var repsMax = row.GetProperty("repsMax").GetByte();
                var targetRir = row.GetProperty("targetRir").GetByte();
                var droppable = row.GetProperty("droppable").GetBoolean();
                var supersetGroup = row.GetProperty("supersetGroup").GetByte();

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.SlotTemplates,
                    seedCode,
                    () => new SlotTemplate
                    {
                        SeedCode = seedCode,
                        ArchetypeId = archetypeId,
                        Order = order,
                        Block = block,
                        AllowedPatternsJson = allowedPatterns,
                        PrimaryMuscleId = primaryMuscleId,
                        SetsMin = setsMin,
                        SetsMax = setsMax,
                        RepsMin = repsMin,
                        RepsMax = repsMax,
                        TargetRir = targetRir,
                        Droppable = droppable,
                        SupersetGroup = supersetGroup
                    },
                    existing =>
                    {
                        existing.ArchetypeId = archetypeId;
                        existing.Order = order;
                        existing.Block = block;
                        existing.AllowedPatternsJson = allowedPatterns;
                        existing.PrimaryMuscleId = primaryMuscleId;
                        existing.SetsMin = setsMin;
                        existing.SetsMax = setsMax;
                        existing.RepsMin = repsMin;
                        existing.RepsMax = repsMax;
                        existing.TargetRir = targetRir;
                        existing.Droppable = droppable;
                        existing.SupersetGroup = supersetGroup;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var row in entityMap.GetValueOrDefault("WeekTemplate", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var daysPerWeek = row.GetProperty("daysPerWeek").GetInt32();
                var sequence = JsonSerializer.Serialize(row.GetProperty("archetypeSequence"));

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.WeekTemplates,
                    seedCode,
                    () => new WeekTemplate
                    {
                        SeedCode = seedCode,
                        DaysPerWeek = daysPerWeek,
                        ArchetypeSequenceJson = sequence
                    },
                    existing =>
                    {
                        existing.DaysPerWeek = daysPerWeek;
                        existing.ArchetypeSequenceJson = sequence;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var row in entityMap.GetValueOrDefault("WorkoutPlanningConfigEntry", []))
            {
                var seedCode = row.GetProperty("seedCode").GetString()!;
                var section = row.GetProperty("section").GetString() ?? "Default";
                var key = row.GetProperty("configKey").GetString() ?? seedCode;

                await PostgresCatalogSeedWriter.UpsertBySeedCodeAsync(
                    context,
                    context.WorkoutPlanningConfigEntries,
                    seedCode,
                    () => new WorkoutPlanningConfigEntry
                    {
                        SeedCode = seedCode,
                        Section = section,
                        Key = key,
                        Value = "1"
                    },
                    existing =>
                    {
                        existing.Section = section;
                        existing.Key = key;
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            var qCount = await context.QuizQuestions.CountAsync(cancellationToken).ConfigureAwait(false);
            var aCount = await context.QuizAnswers.CountAsync(cancellationToken).ConfigureAwait(false);
            var fCount = await context.CatalogFoodItems.CountAsync(cancellationToken).ConfigureAwait(false);
            var mtCount = await context.CatalogMealTemplates.CountAsync(cancellationToken).ConfigureAwait(false);
            var mteCount = await context.CatalogMealTemplateEntries.CountAsync(cancellationToken).ConfigureAwait(false);
            var exCount = await context.Exercises.CountAsync(cancellationToken).ConfigureAwait(false);
            var mgCount = await context.WorkoutMuscleGroups.CountAsync(cancellationToken).ConfigureAwait(false);
            var gtwCount = await context.GoalTierWeights.CountAsync(cancellationToken).ConfigureAwait(false);
            var vbCount = await context.VolumeBudgetRows.CountAsync(cancellationToken).ConfigureAwait(false);
            var wedCount = await context.WorkoutExerciseDefinitions.CountAsync(cancellationToken).ConfigureAwait(false);
            var emcCount = await context.ExerciseMuscleContributions.CountAsync(cancellationToken).ConfigureAwait(false);
            var saCount = await context.SessionArchetypes.CountAsync(cancellationToken).ConfigureAwait(false);
            var stCount = await context.SlotTemplates.CountAsync(cancellationToken).ConfigureAwait(false);
            var wtCount = await context.WeekTemplates.CountAsync(cancellationToken).ConfigureAwait(false);
            var cfgCount = await context.WorkoutPlanningConfigEntries.CountAsync(cancellationToken).ConfigureAwait(false);

            var manifestRowCount = entityMap.Values.Sum(rows => rows.Length);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new CanonicalCatalogSeedSummary(
                qCount,
                aCount,
                fCount,
                mtCount,
                mteCount,
                exCount,
                mgCount,
                gtwCount,
                vbCount,
                wedCount,
                emcCount,
                saCount,
                stCount,
                wtCount,
                cfgCount,
                manifestRowCount,
                manifestSha256);
        }).ConfigureAwait(false);
    }

    internal static int ParseMappedValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
            return numeric;

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            const string EnumCastPrefix = "(int)";
            if (text?.StartsWith(EnumCastPrefix, StringComparison.Ordinal) == true)
            {
                var symbol = text[EnumCastPrefix.Length..];
                var separator = symbol.IndexOf('.');
                if (separator > 0 && separator < symbol.Length - 1)
                {
                    var enumType = symbol[..separator];
                    var member = symbol[(separator + 1)..];
                    return enumType switch
                    {
                        nameof(CyclePhase) => ParseEnumValue<CyclePhase>(member),
                        nameof(DietaryTag) => ParseEnumValue<DietaryTag>(member),
                        nameof(UserGoal) => ParseEnumValue<UserGoal>(member),
                        nameof(WeightGoalPace) => ParseEnumValue<WeightGoalPace>(member),
                        _ => throw new InvalidDataException($"Unsupported quiz answer mapping: {text}.")
                    };
                }
            }
        }

        throw new InvalidDataException("Quiz answer mappedValue must be an integer or a supported enum member.");
    }

    private static int ParseEnumValue<TEnum>(string member) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(member, ignoreCase: false, out var value) && Enum.IsDefined(value))
            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);

        throw new InvalidDataException($"Unsupported {typeof(TEnum).Name} quiz answer member: {member}.");
    }
    private readonly record struct StarterFoodNutrients(
        decimal? Calories, decimal? Protein, decimal? Carbs, decimal? Fats,
        decimal? Fiber, decimal? Iron, decimal? VitaminB12, decimal? VitaminC,
        decimal? VitaminD, decimal? VitaminA, decimal? VitaminB6, decimal? Folate,
        decimal? Calcium, decimal? Magnesium, decimal? Zinc, decimal? Potassium);

    private static readonly Dictionary<string, StarterFoodNutrients> StarterNutrition = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Carrot, raw"] = new(41.0m, 0.9m, 9.6m, 0.2m, 2.8m, 0.3m, null, 5.9m, null, 835.0m, null, null, 33.0m, 12.0m, 0.2m, 320.0m),
        ["Olive oil"] = new(884.0m, null, null, 100.0m, null, null, null, null, null, null, null, null, null, null, null, null),
        ["Oats, rolled, dry"] = new(389.0m, 16.9m, 66.3m, 6.9m, 10.6m, 4.7m, null, null, null, null, null, 56.0m, 54.0m, 177.0m, 3.97m, 429.0m),
        ["Certified gluten-free rolled oats, dry"] = new(389.0m, 16.9m, 66.3m, 6.9m, 10.6m, 4.7m, null, null, null, null, null, 56.0m, 54.0m, 177.0m, 3.97m, 429.0m),
        ["Eggs, whole, raw"] = new(143.0m, 12.6m, 0.7m, 9.5m, null, 1.75m, 0.9m, null, 2.0m, 160.0m, null, null, 56.0m, null, 1.29m, 138.0m),
        ["Chicken breast, raw"] = new(120.0m, 22.5m, null, 2.6m, null, 0.37m, null, null, null, null, null, null, 11.0m, null, null, 256.0m),
        ["Spinach, raw"] = new(23.0m, 2.9m, 3.6m, 0.4m, 2.2m, 2.7m, null, 28.1m, null, 469.0m, null, 194.0m, 99.0m, 79.0m, 0.53m, 558.0m),
        ["Cannellini beans, cooked"] = new(114.0m, 7.3m, 20.5m, 0.5m, 6.3m, 2.5m, null, null, null, null, null, 81.0m, 46.0m, 53.0m, 1.1m, 561.0m),
        ["Black beans, cooked"] = new(132.0m, 8.9m, 23.7m, 0.5m, 8.7m, 2.1m, null, null, null, null, null, 149.0m, 27.0m, 70.0m, 1.1m, 355.0m),
        ["Chickpeas, cooked"] = new(164.0m, 8.9m, 27.4m, 2.6m, 7.6m, 2.9m, null, null, null, null, 0.14m, 172.0m, 49.0m, 48.0m, 1.5m, 291.0m),
        ["Lentils, cooked"] = new(116.0m, 9.0m, 20.0m, 0.4m, 7.9m, 3.3m, null, null, null, null, 0.18m, 181.0m, 19.0m, 36.0m, 1.27m, 369.0m),
        ["Quinoa, cooked"] = new(120.0m, 4.4m, 21.3m, 1.9m, 2.8m, 1.5m, null, null, null, null, 0.12m, 42.0m, 17.0m, 64.0m, 1.1m, 172.0m),
        ["Marinara sauce"] = new(54.0m, 1.5m, 8.4m, 1.5m, 2.0m, 1.0m, null, 8.0m, null, 30.0m, null, null, 30.0m, 14.0m, 0.3m, 360.0m),
        ["Tomato paste"] = new(82.0m, 4.3m, 18.9m, 0.5m, 4.1m, 3.0m, null, 21.9m, null, 76.0m, null, 11.0m, 36.0m, 42.0m, 0.6m, 1014.0m),
        ["Mozzarella cheese, part-skim"] = new(254.0m, 24.0m, 2.8m, 15.9m, null, 0.2m, 1.7m, null, 0.3m, 180.0m, null, null, 782.0m, null, 3.2m, 84.0m),
        ["Parmesan cheese"] = new(431.0m, 38.0m, 4.1m, 29.0m, null, 0.8m, 1.4m, null, 0.5m, 207.0m, null, null, 1184.0m, null, 2.8m, 92.0m),
        ["Turkey pepperoni"] = new(494.0m, 19.0m, 4.0m, 44.0m, null, 1.6m, 1.0m, null, null, null, 0.24m, null, null, null, 2.6m, 276.0m),
        ["Firm tofu, calcium-set"] = new(144.0m, 17.3m, 2.8m, 8.7m, 2.3m, 2.7m, null, null, null, null, null, null, 683.0m, 58.0m, 1.6m, 237.0m),
        ["Nutritional yeast, fortified"] = new(325.0m, 45.0m, 35.0m, 5.0m, 20.0m, 3.0m, 44.0m, null, null, null, 20.0m, 1200.0m, 67.0m, 180.0m, 7.0m, 955.0m),
        ["Red bell pepper, raw"] = new(31.0m, 1.0m, 6.0m, 0.3m, 2.1m, 0.4m, null, 128.0m, null, 157.0m, 0.29m, 46.0m, 7.0m, 12.0m, 0.25m, 211.0m),
        ["Kale, raw"] = new(35.0m, 2.9m, 4.4m, 1.5m, 4.1m, 1.6m, null, 93.0m, null, 241.0m, null, 62.0m, 254.0m, 33.0m, 0.4m, 348.0m),
        ["Sweet potato, baked"] = new(90.0m, 2.0m, 20.7m, 0.2m, 3.3m, 0.7m, null, 19.6m, null, 961.0m, 0.29m, 6.0m, 38.0m, 27.0m, 0.32m, 475.0m),
        ["Avocado, raw"] = new(160.0m, 2.0m, 8.5m, 14.7m, 6.7m, 0.6m, null, 10.0m, null, null, 0.26m, 81.0m, 12.0m, 29.0m, 0.64m, 485.0m),
        ["Greek yogurt, plain nonfat"] = new(59.0m, 10.2m, 3.6m, 0.4m, null, null, 0.75m, null, null, null, null, null, 110.0m, 11.0m, 0.52m, 141.0m),
        ["Cottage cheese, low-fat"] = new(82.0m, 11.5m, 3.4m, 2.3m, null, null, 0.43m, null, null, null, null, null, 83.0m, 8.0m, 0.4m, 104.0m),
        ["Blueberries, raw"] = new(57.0m, 0.7m, 14.5m, 0.3m, 2.4m, 0.3m, null, 9.7m, null, null, 0.05m, 6.0m, 6.0m, 6.0m, 0.16m, 77.0m),
        ["Pumpkin seeds"] = new(559.0m, 30.0m, 10.7m, 49.0m, 6.0m, 8.8m, null, null, null, null, null, null, 46.0m, 592.0m, 7.8m, 809.0m),
        ["Salmon, cooked"] = new(206.0m, 22.0m, null, 12.4m, null, 0.3m, 3.2m, null, 10.9m, null, 0.6m, null, 9.0m, 30.0m, 0.6m, 384.0m),
        ["Tuna, canned in water"] = new(116.0m, 25.5m, null, 0.8m, null, 1.0m, 2.2m, null, 2.0m, null, 0.4m, null, 11.0m, 30.0m, 0.8m, 237.0m),
        ["Hummus"] = new(166.0m, 7.9m, 14.3m, 9.6m, 6.0m, 2.4m, null, null, null, null, 0.2m, 83.0m, 38.0m, 71.0m, 1.4m, 228.0m),
        ["Chia seeds"] = new(486.0m, 16.5m, 42.0m, 30.7m, 34.4m, 7.7m, null, null, null, null, null, null, 631.0m, 335.0m, 4.6m, 407.0m),
        ["Fortified soy milk, unsweetened"] = new(33.0m, 3.3m, 1.6m, 1.8m, 0.6m, 0.6m, 1.2m, null, 1.2m, null, null, null, 123.0m, 15.0m, 0.3m, 118.0m),
        ["White potato, baked"] = new(93.0m, 2.5m, 21.2m, 0.1m, 2.2m, 0.6m, null, 9.6m, null, null, 0.3m, null, 15.0m, 28.0m, 0.3m, 544.0m),
        ["Whole wheat bread"] = new(252.0m, 12.5m, 43.0m, 3.4m, 6.0m, 2.5m, null, null, null, null, null, 44.0m, 107.0m, 75.0m, 1.8m, 254.0m),
        ["Flour tortilla, whole wheat"] = new(291.0m, 8.0m, 46.0m, 8.5m, 4.6m, 2.7m, null, null, null, null, null, null, 180.0m, 38.0m, 0.9m, 170.0m),
        ["Hamburger bun, whole wheat"] = new(260.0m, 9.0m, 46.0m, 4.5m, 4.0m, 2.5m, null, null, null, null, null, 70.0m, 120.0m, 42.0m, 1.1m, 180.0m),
        ["Pinto beans, cooked"] = new(143.0m, 9.0m, 26.2m, 0.6m, 9.0m, 2.1m, null, null, null, null, 0.23m, 172.0m, 46.0m, 50.0m, 0.98m, 436.0m),
        ["Brown rice, cooked"] = new(123.0m, 2.7m, 25.6m, 1.0m, 1.6m, 0.6m, null, null, null, null, 0.15m, null, null, 39.0m, 0.6m, 86.0m),
        ["Turkey ham, sliced"] = new(120.0m, 18.0m, 1.5m, 4.5m, null, 0.8m, 0.8m, null, null, null, 0.3m, null, null, null, 1.8m, 260.0m),
        ["Turkey sausage"] = new(196.0m, 17.0m, 1.0m, 13.5m, null, 1.2m, 1.2m, null, null, null, 0.25m, null, null, null, 2.5m, 230.0m),
        ["Ground turkey, raw"] = new(149.0m, 19.5m, null, 7.7m, null, 1.1m, 1.5m, null, null, null, 0.5m, null, null, null, 3.2m, 280.0m),
        ["Ground beef, 90% lean"] = new(176.0m, 20.0m, null, 10.0m, null, 2.3m, 2.3m, null, null, null, 0.35m, null, null, null, 4.8m, 315.0m),
        ["Ground chicken, raw"] = new(143.0m, 17.4m, null, 8.1m, null, 0.8m, 0.3m, null, null, null, 0.4m, null, null, null, 1.5m, 222.0m),
        ["Lettuce, romaine"] = new(17.0m, 1.2m, 3.3m, 0.3m, 2.1m, 1.0m, null, 24.0m, null, 436.0m, null, 136.0m, 33.0m, 14.0m, 0.2m, 247.0m),
        ["Onion, raw"] = new(40.0m, 1.1m, 9.3m, 0.1m, 1.7m, 0.2m, null, 7.4m, null, null, 0.12m, 19.0m, 23.0m, 10.0m, 0.17m, 146.0m),
        ["Tomato, raw"] = new(18.0m, 0.9m, 3.9m, 0.2m, 1.2m, 0.3m, null, 14.0m, null, 42.0m, 0.08m, 15.0m, 10.0m, 11.0m, 0.17m, 237.0m),
        ["Strawberries, raw"] = new(32.0m, 0.7m, 7.7m, 0.3m, 2.0m, 0.4m, null, 58.8m, null, null, null, 24.0m, 16.0m, 13.0m, 0.14m, 153.0m),
        ["Raspberries, raw"] = new(52.0m, 1.2m, 11.9m, 0.7m, 6.5m, 0.7m, null, 26.2m, null, null, null, 21.0m, 25.0m, 22.0m, 0.42m, 151.0m),
        ["Banana, raw"] = new(89.0m, 1.1m, 22.8m, 0.3m, 2.6m, 0.3m, null, 8.7m, null, null, 0.37m, 20.0m, 5.0m, 27.0m, 0.15m, 358.0m),
        ["Crushed tomatoes, canned"] = new(32.0m, 1.6m, 6.3m, 0.3m, 1.9m, 0.8m, null, 9.0m, null, 25.0m, null, null, 22.0m, 15.0m, 0.2m, 293.0m),
        ["Sriracha sauce"] = new(93.0m, 1.7m, 18.5m, 1.0m, 1.0m, 0.5m, null, 44.0m, null, 130.0m, null, null, 12.0m, null, null, 115.0m),
        ["Sour cream, reduced fat"] = new(135.0m, 3.5m, 5.0m, 11.5m, null, null, 0.3m, null, null, null, null, null, 116.0m, 10.0m, 0.3m, 140.0m),
        ["Yellow mustard"] = new(60.0m, 3.7m, 5.3m, 3.3m, 3.3m, 1.5m, null, null, null, null, null, null, 58.0m, 48.0m, 0.6m, 138.0m),
        ["Ketchup, zero sugar"] = new(15.0m, 0.2m, 3.5m, 0.1m, 0.1m, null, null, null, null, null, null, null, null, null, null, 100.0m),
        ["Hot sauce, generic"] = new(11.0m, 0.5m, 1.8m, 0.4m, 0.5m, 0.6m, null, 12.0m, null, 54.0m, null, null, 10.0m, null, null, 115.0m),
        ["Greek yogurt, plain 2%"] = new(73.0m, 10.0m, 4.5m, 1.5m, null, null, 0.75m, null, null, null, null, null, 115.0m, 11.0m, 0.52m, 141.0m),
        ["Apple cider vinegar"] = new(21.0m, 0.0m, 0.9m, null, null, null, null, null, null, null, null, null, 7.0m, 5.0m, null, 73.0m),
        ["White vinegar, distilled"] = new(18.0m, 0.0m, 0.04m, null, null, null, null, null, null, null, null, null, null, null, null, null),
        ["Salsa, no sugar added"] = new(29.0m, 1.5m, 5.5m, 0.2m, 1.8m, 0.4m, null, 12.0m, null, 20.0m, null, null, 18.0m, 10.0m, null, 280.0m),
        ["Honey"] = new(304.0m, 0.3m, 82.4m, null, null, null, null, null, null, null, null, null, 6.0m, 2.0m, 0.22m, 52.0m),
        ["Peanut butter, natural"] = new(588.0m, 25.0m, 20.0m, 50.0m, 6.0m, 1.9m, null, null, null, null, 0.44m, 87.0m, 43.0m, 168.0m, 2.8m, 649.0m),
        ["Dark chocolate, 70%"] = new(598.0m, 7.8m, 46.0m, 43.0m, 11.0m, 11.9m, null, null, null, null, null, null, 73.0m, 228.0m, 3.3m, 715.0m),
        ["Maple syrup"] = new(260.0m, 0.1m, 67.0m, 0.1m, null, 0.1m, null, null, null, null, null, null, 102.0m, 21.0m, 1.5m, 212.0m),
    };

}
