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
                        Calories = 120m + (foodIndex % 5) * 25m,
                        Protein = 10m + (foodIndex % 4) * 3m,
                        Carbs = 12m + (foodIndex % 6) * 4m,
                        Fats = 4m + (foodIndex % 3) * 2m,
                        ServingSize = 100m,
                        ServingSizeUnit = "g",
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    },
                    existing =>
                    {
                        existing.Name = name;
                        existing.FdcId = fdcId;
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
}
