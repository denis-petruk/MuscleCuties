using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public static class WorkoutExerciseV020CatalogMapper
{
    private const decimal SecondsPerMinute = 60m;

    public static StrengthExerciseCatalogEntryV020 Map(WorkoutExerciseDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var gaps = new List<string>();
        var equipment = MapEquipment(definition.Required, gaps);
        var experiences = ParseEnumSet<ActivityExperienceV020>(definition.SupportedExperiencesV020Json,
            "SupportedExperiences", gaps);
        var demandTags = ParseEnumSet<ActivityDemandTag>(definition.DemandTagsV020Json, "DemandTags", gaps,
            allowEmpty: true);
        var restrictionConflictTags = ParseEnumSet<ActivityDemandTag>(definition.RestrictionConflictTagsV020Json,
            "RestrictionConflictTags", gaps, allowEmpty: true);
        var timingMethods = ParseEnumSet<ExerciseTimingMethod>(definition.SupportedTimingMethodsV020Json,
            "SupportedTimingMethods", gaps);
        var substituteIds = ParseIntegerSet(definition.ReviewedSubstituteExerciseIdsV020Json,
            "ReviewedSubstituteExerciseIds", gaps);
        var catalogVersion = definition.TrainingCatalogVersionV020;
        if (string.IsNullOrWhiteSpace(catalogVersion))
            gaps.Add("TrainingCatalogVersionMissing");
        if (definition.DemandTagsReviewedV020 is not true)
            gaps.Add("DemandTagsNotReviewed");
        if (definition.RestrictionMappingsReviewedV020 is not true)
            gaps.Add("RestrictionMappingsNotReviewed");
        if (!Enum.IsDefined(definition.Pattern))
            gaps.Add("MovementPatternMissingOrInvalid");
        var knownInjuryFlags = Enum.GetValues<InjuryFlag>()
            .Where(flag => flag != InjuryFlag.None)
            .Aggregate(InjuryFlag.None, (current, flag) => current | flag);
        if ((definition.Contraindications & ~knownInjuryFlags) != InjuryFlag.None)
            gaps.Add("ContraindicationMetadataInvalid");

        var candidate = new ReviewedExerciseCandidate(
            definition.Id,
            catalogVersion ?? string.Empty,
            definition.Pattern,
            equipment,
            experiences,
            demandTags,
            definition.DemandTagsReviewedV020 == true,
            definition.RestrictionMappingsReviewedV020 == true,
            restrictionConflictTags,
            timingMethods,
            definition.SetupSeconds >= 0 ? definition.SetupSeconds / SecondsPerMinute : null,
            substituteIds,
            definition.Contraindications);
        return new StrengthExerciseCatalogEntryV020(definition.Name, candidate,
            Array.AsReadOnly(gaps.Distinct(StringComparer.Ordinal).OrderBy(gap => gap, StringComparer.Ordinal).ToArray()));
    }

    private static IReadOnlySet<ExerciseEquipmentKind>? MapEquipment(EquipmentSet required,
        ICollection<string> gaps)
    {
        var result = new HashSet<ExerciseEquipmentKind>();
        if (required == EquipmentSet.None)
        {
            result.Add(ExerciseEquipmentKind.Bodyweight);
            return result;
        }

        MapFlag(EquipmentSet.Barbell, ExerciseEquipmentKind.Barbell);
        MapFlag(EquipmentSet.Dumbbell, ExerciseEquipmentKind.Dumbbell);
        MapFlag(EquipmentSet.Bands, ExerciseEquipmentKind.ResistanceBand);
        MapFlag(EquipmentSet.Cables, ExerciseEquipmentKind.Cable);
        MapFlag(EquipmentSet.Machines, ExerciseEquipmentKind.Machine);
        MapFlag(EquipmentSet.HipThrustBench, ExerciseEquipmentKind.HipThrustBench);
        MapFlag(EquipmentSet.BackExtension45, ExerciseEquipmentKind.BackExtension45);
        MapFlag(EquipmentSet.PullUpBar, ExerciseEquipmentKind.PullUpBar);
        MapFlag(EquipmentSet.Kettlebell, ExerciseEquipmentKind.Kettlebell);
        MapFlag(EquipmentSet.ClimbingWall, ExerciseEquipmentKind.ClimbingWall);

        var knownFlags = Enum.GetValues<EquipmentSet>()
            .Where(flag => flag != EquipmentSet.None)
            .Aggregate(EquipmentSet.None, (current, flag) => current | flag);
        var unknownFlags = required & ~knownFlags;
        if (unknownFlags != EquipmentSet.None)
            gaps.Add($"UnmappedEquipmentFlags:{(int)unknownFlags}");
        return result.Count == 0 ? null : result;

        void MapFlag(EquipmentSet flag, ExerciseEquipmentKind kind)
        {
            if ((required & flag) == flag)
                result.Add(kind);
        }
    }

    private static IReadOnlySet<TEnum>? ParseEnumSet<TEnum>(string? json, string label, ICollection<string> gaps)
        where TEnum : struct, Enum => ParseEnumSet<TEnum>(json, label, gaps, allowEmpty: false);

    private static IReadOnlySet<TEnum>? ParseEnumSet<TEnum>(string? json, string label, ICollection<string> gaps,
        bool allowEmpty)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            gaps.Add($"{label}Missing");
            return null;
        }

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json);
            if (values is null || !allowEmpty && values.Length == 0 ||
                values.Any(value => !Enum.TryParse<TEnum>(value, true, out var parsed) || !Enum.IsDefined(parsed)))
            {
                gaps.Add($"{label}Invalid");
                return null;
            }
            return values.Select(value => Enum.Parse<TEnum>(value, true)).ToHashSet();
        }
        catch (JsonException)
        {
            gaps.Add($"{label}Invalid");
            return null;
        }
    }

    private static IReadOnlySet<int>? ParseIntegerSet(string? json, string label, ICollection<string> gaps)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            gaps.Add($"{label}Missing");
            return null;
        }

        try
        {
            var values = JsonSerializer.Deserialize<int[]>(json);
            if (values is null || values.Any(value => value <= 0))
            {
                gaps.Add($"{label}Invalid");
                return null;
            }
            return values.ToHashSet();
        }
        catch (JsonException)
        {
            gaps.Add($"{label}Invalid");
            return null;
        }
    }
}
