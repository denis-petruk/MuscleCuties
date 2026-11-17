using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.Catalog;
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
        int? distinctCompletedWorkoutDays = null,
        bool? isReturningAfterDetraining = null,
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
        selectedActivities.RemoveWhere(activity => source.RestrictedActivityTypes?.Contains((int)activity) == true);

        var availableWeekdays = (request.AvailableWeekdays ?? source.AvailableWeekdays ?? new HashSet<DayOfWeek>())
            .ToHashSet();

        var durations = (request.ActivitySessionDurationsMinutes ?? source.ActivitySessionDurationsMinutes ?? new Dictionary<int, int>())
            .ToDictionary(pair => (WorkoutActivityType)pair.Key, pair => pair.Value);

        var excludedIds = new HashSet<int>(request.ExcludedExerciseIds ?? new HashSet<int>());
        if (source.ExcludedExerciseIds is not null)
            excludedIds.UnionWith(source.ExcludedExerciseIds);

        var activeRestrictions = new List<PainRestrictionV2>(source.PainRestrictions);
        if (source.ActiveInjuryFlags != InjuryFlag.None)
            activeRestrictions.Add(new PainRestrictionV2(null, null, source.ActiveInjuryFlags,
                null, false));
        if (source.BlockedMovementPatterns is not null)
        {
            foreach (var pattern in source.BlockedMovementPatterns)
            {
                activeRestrictions.Add(new PainRestrictionV2(
                    ExerciseId: null,
                    BlockedPattern: pattern,
                    SiteFlag: source.ActiveInjuryFlags,
                    ReportedAtUtc: null,
                    IsResolved: false));
            }
        }

        var v2Catalog = MapCatalog(source);
        var style = ResolveStrengthStyle(request, selectedActivities);
        var history = request.ActivityHistory is { } reported ? new ActivityTrainingHistory(reported.ActivityId,
            reported.ConsistentParticipationWeeks, reported.AverageSessionsPerWeekInRecentFourWeeks,
            reported.LastCompletedSessionDate, reported.IsSelfReported) : null;
        var effectiveExperience = WorkoutExperiencePromotionRule.Resolve(
            source.ProfileTrainingExperience ?? TrainingExperienceLevel.Beginner,
            distinctCompletedWorkoutDays ?? source.DistinctCompletedWorkoutDays);
        var frequency = GeneralStrengthFrequencyResolver.Resolve(availableWeekdays.Count,
            GeneralStrengthFrequencyResolver.IsEstablishedResistanceHistory(history, request.LocalDate),
            source.ProfileTrainingExperience);
        var requestedDays = TryGetPositiveInt(request.RequestedSessionsPerWeek, (int)style)
            ?? TryGetPositiveInt(request.RequestedSessionsPerWeek, (int)request.Activity)
            ?? TryGetPositiveInt(source.RequestedSessionsPerWeek, (int)style)
            ?? TryGetPositiveInt(source.RequestedSessionsPerWeek, (int)request.Activity)
            ?? (effectiveExperience == TrainingExperienceLevel.Beginner ? frequency.TargetSessionsPerWeek : (int?)null);
        var localDate = source.CurrentAccountLocalDate ?? request.LocalDate;
        var priorWeekSets = source.PriorObservedSets.Where(set => set.SetKind == SetKindV2.Working &&
            set.RepsCompleted > 0 && set.LocalDate >= localDate.AddDays(-WorkoutPolicyV2.RollingLoadWindowDays) &&
            set.LocalDate < localDate).ToArray();
        var focus = source.GluteFocus
            ? TrainingFocus.GluteSpecialization
            : TrainingFocus.Balanced;

        var input = new WorkoutGenerationInputV2(
            AccountLocalDate: source.CurrentAccountLocalDate ?? request.LocalDate,
            AccountTimeZoneId: source.AccountTimeZoneId,
            BirthDate: birthDate ?? source.BirthDate,
            PregnancyStatus: source.PregnancyStatus != PregnancyStatus.No ? source.PregnancyStatus : pregnancyStatus,
            EffectiveExperience: source.ProfileTrainingExperience ?? TrainingExperienceLevel.Beginner,
            DistinctCompletedWorkoutDays: distinctCompletedWorkoutDays ?? source.DistinctCompletedWorkoutDays,
            IsReturningAfterDetraining: isReturningAfterDetraining ?? source.IsReturningAfterDetraining,
            Goal: source.Goal,
            Focus: focus,
            StrengthStyle: style,
            SelectedActivities: selectedActivities,
            AvailableWeekdays: availableWeekdays,
            SessionDurationMinutesByActivity: durations,
            EquipmentProfile: source.Equipment,
            AvailableEquipment: StrengthPlanPreviewRule.AvailableEquipmentFor(
                source.Equipment, source.DraftEquipmentCapabilities ?? source.SavedAnswers?.EquipmentCapabilities),
            Catalog: v2Catalog,
            PermanentlyExcludedExerciseIds: excludedIds,
            ActivePainRestrictions: activeRestrictions,
            PriorObservedSets: source.PriorObservedSets,
            PriorCardioActuals: source.PriorCardioActuals,
            DailyReadiness: dailyReadiness ?? source.DailyReadiness,
            CycleContext: cycleContext,
            RequestedStrengthDaysPerWeek: requestedDays,
            PriorWeekExerciseIds: priorWeekSets.Select(set => set.ExerciseId).Distinct().Order().ToArray(),
            PreviousWeekStrengthDays: priorWeekSets.Select(set => set.LocalDate).ToHashSet());

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

    private static IReadOnlyList<ExerciseCatalogItemV2> MapCatalog(StrengthPlanPreviewSourceV020 source)
    {
        var list = new List<ExerciseCatalogItemV2>(source.Catalog.Count);
        foreach (var entry in source.Catalog)
        {
            if (entry?.Candidate is not { } candidate)
                continue;

            var experiences = candidate.SupportedExperiences?
                .SelectMany(experience => experience == ActivityExperience.Established
                    ? new[] { TrainingExperienceLevel.Intermediate, TrainingExperienceLevel.Advanced }
                    : new[] { MapExperience(experience) })
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
            var isBodyweightOnly = requiredEquip.All(eq =>
                eq is ExerciseEquipmentKind.Bodyweight or ExerciseEquipmentKind.Sliders);
            var usesKilogramLoading = requiredEquip.Any(eq =>
                eq is ExerciseEquipmentKind.Barbell or ExerciseEquipmentKind.Dumbbell or
                ExerciseEquipmentKind.Kettlebell or ExerciseEquipmentKind.Machine or ExerciseEquipmentKind.Cable);
            var secondsPerRep = source.SecondsPerRepByExerciseId.TryGetValue(candidate.ExerciseId, out var spr) && spr > 0 ? spr : 0;
            var isCorePattern = candidate.Pattern is
                MovementPattern.AntiExtension or MovementPattern.AntiRotation or MovementPattern.AntiLateralFlexion;
            var supportsStraightSets = candidate.SupportedTimingMethods is null ||
                candidate.SupportedTimingMethods.Contains(ExerciseTimingMethod.StraightSets);
            var varietyTag = isCorePattern
                ? (supportsStraightSets ? ExerciseVarietyTag.DynamicCore : ExerciseVarietyTag.StaticCore)
                : ExerciseVarietyTag.None;

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
                MinimumLoadIncrementKg: usesKilogramLoading ? 2.5m : null,
                VarietyTag: varietyTag,
                ReviewStatus: !string.IsNullOrWhiteSpace(candidate.CatalogVersion) &&
                    candidate.ReviewEvidencePresent &&
                    entry.MetadataGaps is { Count: 0 }
                    ? CatalogReviewStatusV2.ApprovedWithConditions
                    : CatalogReviewStatusV2.PendingOwnerReview,
                ReviewVersion: candidate.CatalogVersion,
                IsGluteDominant: candidate.Pattern is MovementPattern.HipThrust or MovementPattern.HipAbduction,
                SupportedTimingMethods: candidate.SupportedTimingMethods));
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
        if (source.MuscleContributionsByExerciseId is not null &&
            source.MuscleContributionsByExerciseId.TryGetValue(exerciseId, out var rows) &&
            rows.Count > 0)
        {
            var collapsed = new Dictionary<MuscleGroupV2, decimal>();
            foreach (var row in rows)
            {
                if (row.Fraction <= 0m || MuscleAccountingTaxonomy.Map(row.MuscleGroup, pattern) is not { } target)
                    continue;

                var mapped = MapTarget(target);
                collapsed[mapped] = collapsed.TryGetValue(mapped, out var existing)
                    ? Math.Max(existing, row.Fraction)
                    : row.Fraction;
            }

            if (collapsed.Count > 0)
            {
                return collapsed
                    .Select(pair => pair.Value >= 1.0m
                        ? ExerciseMuscleContributionV2.Direct(pair.Key)
                        : ExerciseMuscleContributionV2.Indirect(pair.Key, pair.Value))
                    .ToArray();
            }
        }

        var directMuscle = source.PrimaryMuscleByExerciseId.TryGetValue(exerciseId, out var primary) &&
            TryMapMuscleName(primary, pattern, out var mappedPrimary)
            ? mappedPrimary
            : (MuscleGroupV2?)null;

        if (source.MuscleGroupsByExerciseId is not null &&
            source.MuscleGroupsByExerciseId.TryGetValue(exerciseId, out var groupNames) &&
            groupNames.Count > 0)
        {
            var contributions = new Dictionary<MuscleGroupV2, decimal>();
            foreach (var name in groupNames)
            {
                if (!TryMapMuscleName(name, pattern, out var mapped))
                    continue;

                var credit = directMuscle == mapped ? 1.0m : 0.5m;
                contributions[mapped] = contributions.TryGetValue(mapped, out var existing)
                    ? Math.Max(existing, credit)
                    : credit;
            }

            if (directMuscle is { } dm && !contributions.ContainsKey(dm))
                contributions[dm] = 1.0m;

            if (contributions.Count > 0)
            {
                return contributions
                    .Select(pair => pair.Value >= 1.0m
                        ? ExerciseMuscleContributionV2.Direct(pair.Key)
                        : ExerciseMuscleContributionV2.Indirect(pair.Key, pair.Value))
                    .ToArray();
            }
        }

        if (directMuscle is { } onlyPrimary)
            return [ExerciseMuscleContributionV2.Direct(onlyPrimary)];

        return pattern switch
        {
            MovementPattern.HipThrust => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.GluteusMaximus)],
            MovementPattern.SquatPattern or MovementPattern.Lunge =>
                [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Quadriceps), ExerciseMuscleContributionV2.Indirect(MuscleGroupV2.GluteusMaximus)],
            MovementPattern.HipHinge or MovementPattern.KneeFlexion => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.Hamstrings)],
            MovementPattern.HipAbduction => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.HipAbductors)],
            MovementPattern.HipAdduction => [ExerciseMuscleContributionV2.Direct(MuscleGroupV2.HipAdductors)],
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

    private static MuscleGroupV2 MapTarget(MuscleAccountingTarget target) => target switch
    {
        MuscleAccountingTarget.GluteMaximus => MuscleGroupV2.GluteusMaximus,
        MuscleAccountingTarget.HipAbductors => MuscleGroupV2.HipAbductors,
        MuscleAccountingTarget.Adductors => MuscleGroupV2.HipAdductors,
        MuscleAccountingTarget.Quadriceps => MuscleGroupV2.Quadriceps,
        MuscleAccountingTarget.Hamstrings => MuscleGroupV2.Hamstrings,
        MuscleAccountingTarget.Back => MuscleGroupV2.Back,
        MuscleAccountingTarget.Chest => MuscleGroupV2.Chest,
        MuscleAccountingTarget.AnteriorDelts => MuscleGroupV2.AnteriorDelts,
        MuscleAccountingTarget.LateralDelts => MuscleGroupV2.LateralDelts,
        MuscleAccountingTarget.RearDelts => MuscleGroupV2.RearDelts,
        MuscleAccountingTarget.Biceps => MuscleGroupV2.Biceps,
        MuscleAccountingTarget.Triceps => MuscleGroupV2.Triceps,
        MuscleAccountingTarget.Calves => MuscleGroupV2.Calves,
        MuscleAccountingTarget.Core => MuscleGroupV2.Core,
        _ => MuscleGroupV2.Core
    };

    private static bool TryMapMuscleName(string rawName, MovementPattern pattern, out MuscleGroupV2 muscle)
    {
        var normalized = rawName.Trim().ToLowerInvariant();
        if (normalized is "delts" or "shoulders")
        {
            muscle = MapTarget(MuscleAccountingTaxonomy.Map("Delts", pattern) ?? MuscleAccountingTarget.AnteriorDelts);
            return true;
        }

        muscle = normalized switch
        {
            "glutes" or "gluteus" or "gluteusmaximus" or "gluteus_maximus" => MuscleGroupV2.GluteusMaximus,
            "quads" or "quadriceps" => MuscleGroupV2.Quadriceps,
            "hamstrings" => MuscleGroupV2.Hamstrings,
            "back" or "lats" or "upperback" or "upper_back" => MuscleGroupV2.Back,
            "chest" or "pectorals" => MuscleGroupV2.Chest,
            "lateral_delts" or "lateraldelts" => MuscleGroupV2.LateralDelts,
            "rear_delts" or "reardelts" => MuscleGroupV2.RearDelts,
            "anterior_delts" or "anteriordelts" => MuscleGroupV2.AnteriorDelts,
            "biceps" => MuscleGroupV2.Biceps,
            "triceps" => MuscleGroupV2.Triceps,
            "calves" => MuscleGroupV2.Calves,
            "core" or "abs" => MuscleGroupV2.Core,
            "abductors" or "hip_abductors" or "hipabductors" or "glute_medius" => MuscleGroupV2.HipAbductors,
            "adductors" or "hip_adductors" or "hipadductors" => MuscleGroupV2.HipAdductors,
            _ => default
        };

        return normalized is "glutes" or "gluteus" or "gluteusmaximus" or "gluteus_maximus"
            or "quads" or "quadriceps" or "hamstrings" or "back" or "lats" or "upperback" or "upper_back"
            or "chest" or "pectorals" or "lateral_delts" or "lateraldelts"
            or "rear_delts" or "reardelts" or "anterior_delts" or "anteriordelts"
            or "biceps" or "triceps" or "calves" or "core" or "abs"
            or "abductors" or "hip_abductors" or "hipabductors" or "glute_medius"
            or "adductors" or "hip_adductors" or "hipadductors";
    }

    private static int? TryGetPositiveInt(IReadOnlyDictionary<int, int>? values, int key) =>
        values is not null && values.TryGetValue(key, out var count) && count > 0 ? count : null;
}
