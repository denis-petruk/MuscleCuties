using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>
/// Application-layer adapter bridging existing trusted planning source contracts into
/// <see cref="WorkoutGenerationEngineV2"/> without touching database or UI layers directly.
/// </summary>
public static class WorkoutGenerationV2Service
{
    public static WorkoutGenerationResultV2 GenerateFromPreviewSource(
        StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewSourceV020 source,
        DateOnly? birthDate = null,
        PregnancyStatus pregnancyStatus = PregnancyStatus.No,
        string timeZoneId = "UTC",
        int distinctCompletedWorkoutDays = 0,
        bool isReturningAfterDetraining = false,
        DailyReadinessInputV2? dailyReadiness = null,
        CycleContextInputV2? cycleContext = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        var selectedActivities = (request.SelectedActivityTypes ?? source.SelectedActivityTypes ?? new HashSet<int>())
            .Select(value => (WorkoutActivityType)value)
            .Where(Enum.IsDefined)
            .ToHashSet();

        if (selectedActivities.Count == 0)
            selectedActivities.Add(WorkoutActivityContractMapper.ToCore(request.Activity));

        var availableWeekdays = (request.AvailableWeekdays ?? source.AvailableWeekdays ?? new HashSet<DayOfWeek>())
            .ToHashSet();

        var durations = (request.ActivitySessionDurationsMinutes ?? source.ActivitySessionDurationsMinutes ?? new Dictionary<int, int>())
            .ToDictionary(pair => (WorkoutActivityType)pair.Key, pair => pair.Value);

        var excludedIds = new HashSet<int>(request.ExcludedExerciseIds ?? new HashSet<int>());
        if (source.ExcludedExerciseIds is not null)
            excludedIds.UnionWith(source.ExcludedExerciseIds);

        var activeRestrictions = new List<PainRestrictionV2>();
        if (source.BlockedMovementPatterns is not null)
        {
            var reportedAt = new DateTimeOffset(request.LocalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            foreach (var pattern in source.BlockedMovementPatterns)
            {
                activeRestrictions.Add(new PainRestrictionV2(
                    ExerciseId: null,
                    BlockedPattern: pattern,
                    SiteFlag: source.ActiveInjuryFlags,
                    ReportedAtUtc: reportedAt,
                    IsResolved: false));
            }
        }

        var v2Catalog = MapCatalog(source);
        var style = ResolveStrengthStyle(request, selectedActivities);
        var focus = source.GluteFocus
            ? TrainingFocus.GluteSpecialization
            : TrainingFocus.Balanced;

        var input = new WorkoutGenerationInputV2(
            AccountLocalDate: source.CurrentAccountLocalDate ?? request.LocalDate,
            AccountTimeZoneId: string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId,
            BirthDate: birthDate,
            PregnancyStatus: pregnancyStatus,
            EffectiveExperience: source.ProfileTrainingExperience ?? TrainingExperienceLevel.Beginner,
            DistinctCompletedWorkoutDays: distinctCompletedWorkoutDays,
            IsReturningAfterDetraining: isReturningAfterDetraining,
            Goal: source.Goal,
            Focus: focus,
            StrengthStyle: style,
            SelectedActivities: selectedActivities,
            AvailableWeekdays: availableWeekdays,
            SessionDurationMinutesByActivity: durations,
            EquipmentProfile: source.Equipment,
            AvailableEquipment: MapAvailableEquipment(source.Equipment),
            Catalog: v2Catalog,
            PermanentlyExcludedExerciseIds: excludedIds,
            ActivePainRestrictions: activeRestrictions,
            DailyReadiness: dailyReadiness,
            CycleContext: cycleContext);

        return WorkoutGenerationEngineV2.Generate(input);
    }

    private static StrengthStyleV2 ResolveStrengthStyle(
        StrengthPlanPreviewRequestV020 request,
        IReadOnlySet<WorkoutActivityType> selectedActivities)
    {
        var chosen = request.ExplicitStrengthStyleChoice is { } explicitChoice
            ? WorkoutActivityContractMapper.ToCore(explicitChoice)
            : selectedActivities.FirstOrDefault(WorkoutPreferencesRule.IsStrengthStyle);

        if (chosen == default)
            chosen = WorkoutActivityContractMapper.ToCore(request.Activity);

        return chosen switch
        {
            WorkoutActivityType.HighVolumeStrength => StrengthStyleV2.HighVolumeStrength,
            WorkoutActivityType.StrengthHighIntensity => StrengthStyleV2.StrengthHighIntensity,
            _ => StrengthStyleV2.GeneralStrength
        };
    }

    private static IReadOnlySet<ExerciseEquipmentKind> MapAvailableEquipment(Equipment equipment) => equipment switch
    {
        Equipment.Bodyweight => new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
        Equipment.HomeDumbbellsBands => new HashSet<ExerciseEquipmentKind>
        {
            ExerciseEquipmentKind.Bodyweight,
            ExerciseEquipmentKind.Dumbbell,
            ExerciseEquipmentKind.ResistanceBand,
            ExerciseEquipmentKind.Kettlebell
        },
        _ => Enum.GetValues<ExerciseEquipmentKind>().ToHashSet()
    };

    private static IReadOnlyList<ExerciseCatalogItemV2> MapCatalog(StrengthPlanPreviewSourceV020 source)
    {
        var list = new List<ExerciseCatalogItemV2>(source.Catalog.Count);
        foreach (var entry in source.Catalog)
        {
            if (entry?.Candidate is not { } candidate)
                continue;

            var experiences = candidate.SupportedExperiences?
                .Select(MapExperience)
                .Where(exp => exp != TrainingExperienceLevel.Unknown)
                .ToHashSet() ?? [TrainingExperienceLevel.Beginner, TrainingExperienceLevel.Intermediate, TrainingExperienceLevel.Advanced];

            var muscles = MapMuscleContributions(candidate.ExerciseId, candidate.Pattern, source);
            var isCompound = candidate.Pattern is
                MovementPattern.SquatPattern or
                MovementPattern.HipHinge or
                MovementPattern.HipThrust or
                MovementPattern.HorizontalPush or
                MovementPattern.VerticalPush or
                MovementPattern.HorizontalPull or
                MovementPattern.VerticalPull or
                MovementPattern.Lunge;

            var requiredEquip = candidate.RequiredEquipment ?? new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight };
            var isBodyweightOnly = requiredEquip.Count == 1 && requiredEquip.Contains(ExerciseEquipmentKind.Bodyweight);
            var secondsPerRep = source.SecondsPerRepByExerciseId.TryGetValue(candidate.ExerciseId, out var spr) && spr > 0 ? spr : 3;

            list.Add(new ExerciseCatalogItemV2(
                ExerciseId: candidate.ExerciseId,
                Name: entry.Name,
                Pattern: candidate.Pattern,
                MuscleContributions: muscles,
                RequiredEquipment: requiredEquip,
                SupportedExperiences: experiences,
                Contraindications: candidate.Contraindications ?? InjuryFlag.None,
                IsCompound: isCompound,
                IsUnilateral: candidate.UnilateralDoublesTime,
                IsBodyweightOnly: isBodyweightOnly,
                SetupMinutes: candidate.SetupMinutes ?? 1.0m,
                SecondsPerRep: secondsPerRep,
                MinimumLoadIncrementKg: isBodyweightOnly ? null : 2.5m,
                ReviewStatus: entry.MetadataGaps is { Count: 0 }
                    ? CatalogReviewStatusV2.ApprovedWithConditions
                    : CatalogReviewStatusV2.PendingOwnerReview,
                ReviewVersion: candidate.CatalogVersion,
                IsGluteDominant: candidate.Pattern is MovementPattern.HipThrust or MovementPattern.HipAbduction));
        }

        return list;
    }

    private static TrainingExperienceLevel MapExperience(ActivityExperience experience) => experience switch
    {
        ActivityExperience.Beginner => TrainingExperienceLevel.Beginner,
        ActivityExperience.Returning => TrainingExperienceLevel.Beginner,
        ActivityExperience.Established => TrainingExperienceLevel.Intermediate,
        _ => TrainingExperienceLevel.Unknown
    };

    private static IReadOnlyList<ExerciseMuscleContributionV2> MapMuscleContributions(
        int exerciseId,
        MovementPattern pattern,
        StrengthPlanPreviewSourceV020 source)
    {
        if (source.PrimaryMuscleByExerciseId.TryGetValue(exerciseId, out var primary) &&
            TryMapMuscleName(primary, out var mappedPrimary))
        {
            return [ExerciseMuscleContributionV2.Direct(mappedPrimary)];
        }

        return pattern switch
        {
            MovementPattern.HipThrust => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.GluteusMaximus)],
            MovementPattern.SquatPattern or MovementPattern.Lunge =>
                [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Quadriceps), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.GluteusMaximus)],
            MovementPattern.HipHinge or MovementPattern.KneeFlexion => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Hamstrings)],
            MovementPattern.HipAbduction => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.HipAbductors)],
            MovementPattern.HorizontalPush or MovementPattern.VerticalPush => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Chest)],
            MovementPattern.HorizontalPull or MovementPattern.VerticalPull => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Back)],
            MovementPattern.RearDelt => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.RearDelts)],
            MovementPattern.LateralRaise => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.LateralDelts)],
            MovementPattern.ElbowFlexion => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Biceps)],
            MovementPattern.ElbowExtension => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Triceps)],
            MovementPattern.CalfRaise => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Calves)],
            MovementPattern.AntiExtension or MovementPattern.AntiRotation or MovementPattern.AntiLateralFlexion =>
                [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Core)],
            _ => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Core)]
        };
    }

    private static bool TryMapMuscleName(string rawName, out MuscleGroupV2 muscle)
    {
        var normalized = rawName.Trim().ToLowerInvariant();
        muscle = normalized switch
        {
            "glutes" or "gluteus" or "gluteusmaximus" or "gluteus_maximus" => MuscleGroupV2.GluteusMaximus,
            "quads" or "quadriceps" => MuscleGroupV2.Quadriceps,
            "hamstrings" => MuscleGroupV2.Hamstrings,
            "back" or "lats" => MuscleGroupV2.Back,
            "chest" or "pectorals" => MuscleGroupV2.Chest,
            "lateral_delts" or "lateraldelts" or "shoulders" => MuscleGroupV2.LateralDelts,
            "rear_delts" or "reardelts" => MuscleGroupV2.RearDelts,
            "biceps" => MuscleGroupV2.Biceps,
            "triceps" => MuscleGroupV2.Triceps,
            "calves" => MuscleGroupV2.Calves,
            "core" or "abs" => MuscleGroupV2.Core,
            "hip_abductors" or "hipabductors" or "glute_medius" => MuscleGroupV2.HipAbductors,
            _ => default
        };

        return normalized is "glutes" or "gluteus" or "gluteusmaximus" or "gluteus_maximus"
            or "quads" or "quadriceps" or "hamstrings" or "back" or "lats" or "chest" or "pectorals"
            or "lateral_delts" or "lateraldelts" or "shoulders" or "rear_delts" or "reardelts"
            or "biceps" or "triceps" or "calves" or "core" or "abs"
            or "hip_abductors" or "hipabductors" or "glute_medius";
    }
}
