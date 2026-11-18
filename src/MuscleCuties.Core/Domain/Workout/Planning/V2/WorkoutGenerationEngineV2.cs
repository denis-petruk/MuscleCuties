using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.V2;

/// <summary>
/// Canonical deterministic Workout Engine 2.0 planner.
/// Pure function over explicit, validated inputs (no DB, no clock, no randomness).
/// </summary>
public static class WorkoutGenerationEngineV2
{
    public const string UnsupportedInputContractVersionCode = "UnsupportedInputContractVersion";
    public const string UnsupportedPolicyVersionCode = "UnsupportedPolicyVersion";
    public const string HiitDeferredInV2Code = "HiitDeferredInWorkoutEngineV2";
    public const string SystemicRedFlagSafetyStopCode = "SystemicRedFlagRequiresMedicalAdvice";
    public const string NoSafeStrengthContentCode = "NoSafeStrengthContentRemaining";
    public const string PartialCoverageTimeLimitedCode = "PartialCoverageDueToSessionDuration";
    public const string PartialCoverageEquipmentLimitedCode = "PartialCoverageDueToEquipmentOrCatalog";
    public const string OneDayFullBodyAdvisoryCode = "OneDayFullBodyHasLimitedWeeklyCoverage";
    public const string UnselectedCardioIgnoredCode = "UnselectedActivitiesNotScheduled";
    public const string ReadinessOptionalSetReducedExplanation =
        "Today's optional volume was reduced because of reported fatigue. Main exercises and working weights are unchanged.";

    private const decimal SecondsPerMinute = 60m;
    private const decimal WarmupSetDurationMinutes = 1.0m;
    private const decimal SupersetRestSavingFactor = 0.65m;

    public static WorkoutGenerationResultV2 Generate(WorkoutGenerationInputV2? input)
    {
        if (input is null)
            return InvalidResult(WorkoutGenerationStatusV2.InvalidInput, ["InvalidGenerationInput"],
                WorkoutPolicyV2.Version, string.Empty);

        var trace = new List<string>();
        var limitations = new List<string>();
        var advisories = new List<string>();

        if (!string.Equals(input.InputContractVersion, WorkoutPolicyV2.InputContractVersion, StringComparison.Ordinal))
            return InvalidResult(WorkoutGenerationStatusV2.InvalidInput, [UnsupportedInputContractVersionCode],
                input.PolicyVersion, input.CatalogVersion);

        if (!string.Equals(input.PolicyVersion, WorkoutPolicyV2.Version, StringComparison.Ordinal))
            return InvalidResult(WorkoutGenerationStatusV2.InvalidInput, [UnsupportedPolicyVersionCode],
                input.PolicyVersion, input.CatalogVersion);

        // Gate 85 (1): Safety & Population Scope
        if (input.DailyReadiness?.HasSystemicRedFlagSymptoms == true)
        {
            trace.Add("SafetyStop:SystemicRedFlag");
            return InvalidResult(WorkoutGenerationStatusV2.SafetyBlocked, [SystemicRedFlagSafetyStopCode],
                input.PolicyVersion, input.CatalogVersion);
        }

        var popLimitation = WorkoutPopulationScopeRule.Limitation(
            input.BirthDate, input.AccountLocalDate, input.PregnancyStatus);
        if (popLimitation is not null)
        {
            trace.Add($"PopulationScopeBlocked:{popLimitation}");
            return InvalidResult(WorkoutGenerationStatusV2.SafetyBlocked, [popLimitation],
                input.PolicyVersion, input.CatalogVersion);
        }

        if (input.AvailableWeekdays is null || input.AvailableWeekdays.Count is < 1 or > 7 ||
            input.AvailableWeekdays.Any(d => !Enum.IsDefined(d)))
            return InvalidResult(WorkoutGenerationStatusV2.InvalidInput, ["AvailableWeekdaysMissingOrInvalid"],
                input.PolicyVersion, input.CatalogVersion);

        if (input.Catalog is null || input.Catalog.Select(c => c.ExerciseId).Distinct().Count() != input.Catalog.Count)
            return InvalidResult(WorkoutGenerationStatusV2.InvalidInput, ["InvalidOrDuplicateCatalog"],
                input.PolicyVersion, input.CatalogVersion);

        // Gate 80: HIIT deferred from Engine 2.0 active generation
        if (input.SelectedActivities.Contains(WorkoutActivityType.Hiit))
            advisories.Add(HiitDeferredInV2Code);

        var styleSpec = WorkoutPolicyV2.GetStyleSpec(input.StrengthStyle);
        var effectiveExperience = ResolveEffectiveExperience(input);
        trace.Add($"Experience:{effectiveExperience};Focus:{input.Focus};Style:{input.StrengthStyle}");

        // Filter safe & eligible exercises (no unreviewed, no neck by default, no pain-restricted)
        var nowUtc = new DateTimeOffset(input.AccountLocalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var activeRestrictions = (input.ActivePainRestrictions ?? [])
            .Where(r => !r.IsResolved)
            .ToArray();
        var excludedIds = input.PermanentlyExcludedExerciseIds ?? new HashSet<int>();

        var eligibleCatalog = input.Catalog
            .Where(item => IsExerciseEligible(item, input, effectiveExperience, excludedIds, activeRestrictions))
            .OrderBy(item => item.ExerciseId)
            .ToArray();

        if (eligibleCatalog.Length == 0)
        {
            limitations.Add(NoSafeStrengthContentCode);
            return new WorkoutGenerationResultV2(
                WorkoutGenerationStatusV2.Limited,
                [],
                [],
                null,
                limitations.ToArray(),
                advisories.ToArray(),
                trace.ToArray(),
                input.PolicyVersion,
                input.CatalogVersion,
                ComputeHash([], limitations, advisories));
        }

        // Determine strength training days (availability != automatic 7 strength days)
        var orderedWeekdays = OrderWeekdays(input.AvailableWeekdays);
        var strengthDaysCount = DetermineStrengthDaysCount(orderedWeekdays.Length, effectiveExperience, input);
        var strengthWeekdays = SelectSpacedWeekdays(orderedWeekdays, strengthDaysCount)
            .OrderBy(day => DateForWeekdayInCurrentWeek(input.AccountLocalDate, day)).ToArray();
        if (strengthDaysCount == 1)
            advisories.Add(OneDayFullBodyAdvisoryCode);

        var splitKind = DetermineSplitKind(strengthDaysCount);
        var sessionDurationBudget = ResolveStrengthSessionDurationMinutes(input);

        // Build strength sessions dynamically (no arbitrary 6-exercise cap)
        var sessions = BuildStrengthSessions(
            input,
            effectiveExperience,
            styleSpec,
            eligibleCatalog,
            strengthWeekdays,
            splitKind,
            sessionDurationBudget,
            activeRestrictions,
            nowUtc,
            limitations,
            advisories,
            trace);

        // Schedule selected ordinary cardio (Gate 78-83; HIIT excluded)
        sessions = ScheduleSelectedCardio(input, sessions, orderedWeekdays, advisories, trace);

        var catalogById = input.Catalog.ToDictionary(c => c.ExerciseId);
        var coverageSummary = MuscleAccountingRuleV2.Evaluate(sessions, catalogById, effectiveExperience, input.Focus);

        CheckCoverageGaps(coverageSummary, input, sessions, limitations, advisories);

        // Gate 84, 89, 90, 92: Readiness adaptation (DOMS and phase alone never alter dose)
        var (adaptedSessions, readinessApplied, coachExplanation) = ApplyReadinessAdaptation(
            sessions, input, catalogById, trace);

        var finalStatus = limitations.Count == 0
            ? WorkoutGenerationStatusV2.Feasible
            : WorkoutGenerationStatusV2.Limited;

        var distinctLimitations = limitations.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var distinctAdvisories = advisories.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var hash = ComputeHash(adaptedSessions, distinctLimitations, distinctAdvisories);

        return new WorkoutGenerationResultV2(
            finalStatus,
            sessions,
            adaptedSessions,
            coverageSummary,
            distinctLimitations,
            distinctAdvisories,
            trace.ToArray(),
            input.PolicyVersion,
            input.CatalogVersion,
            hash,
            readinessApplied,
            coachExplanation);
    }

    private static TrainingExperienceLevel ResolveEffectiveExperience(WorkoutGenerationInputV2 input)
    {
        var promoted = WorkoutExperiencePromotionRule.Resolve(
            input.EffectiveExperience, input.DistinctCompletedWorkoutDays);
        return promoted == TrainingExperienceLevel.Unknown ? TrainingExperienceLevel.Beginner : promoted;
    }

    private static bool IsExerciseEligible(
        ExerciseCatalogItemV2 item,
        WorkoutGenerationInputV2 input,
        TrainingExperienceLevel effectiveExperience,
        IReadOnlySet<int> excludedIds,
        PainRestrictionV2[] activeRestrictions)
    {
        if (item.ReviewStatus != CatalogReviewStatusV2.ApprovedWithConditions)
            return false;

        // Timed-only core holds / isometric holds remain optional outside repetition straight-set slots.
        if (item.SupportedTimingMethods is { Count: > 0 } timingMethods &&
            !timingMethods.Contains(ExerciseTimingMethod.StraightSets))
            return false;

        // Gate 53: No routine automatic neck prescription
        if (item.VarietyTag == ExerciseVarietyTag.Neck)
            return false;

        if (excludedIds.Contains(item.ExerciseId))
            return false;

        if (!item.RequiredEquipment.IsSubsetOf(input.AvailableEquipment))
            return false;

        if (item.SupportedExperiences.Count > 0 && !item.SupportedExperiences.Contains(effectiveExperience))
            return false;

        foreach (var restriction in activeRestrictions)
        {
            if (restriction.ExerciseId == item.ExerciseId)
                return false;
            if (restriction.BlockedPattern == item.Pattern)
                return false;
            if (restriction.SiteFlag != InjuryFlag.None && (item.Contraindications & restriction.SiteFlag) != InjuryFlag.None)
                return false;
        }

        return true;
    }

    private static int DetermineStrengthDaysCount(
        int availableDays,
        TrainingExperienceLevel experience,
        WorkoutGenerationInputV2 input)
    {
        if (availableDays <= 1)
            return 1;

        if (input.IsReturningAfterDetraining)
            return Math.Min(availableDays, 2);

        var tierMaximum = experience switch
        {
            TrainingExperienceLevel.Beginner => 3,
            TrainingExperienceLevel.Intermediate => 4,
            TrainingExperienceLevel.Advanced => 5,
            _ => 2
        };
        var requested = input.RequestedStrengthDaysPerWeek is >= 1
            ? input.RequestedStrengthDaysPerWeek.Value : tierMaximum;
        return Math.Min(availableDays, Math.Min(requested, tierMaximum));
    }

    private static SplitKindV2 DetermineSplitKind(int strengthDays) => strengthDays switch
    {
        <= 3 => SplitKindV2.FullBody,
        4 => SplitKindV2.UpperLower,
        _ => SplitKindV2.Hybrid
    };

    private static DayOfWeek[] SelectSpacedWeekdays(DayOfWeek[] orderedAvailable, int count)
    {
        if (count >= orderedAvailable.Length)
            return orderedAvailable;

        if (count == 1)
            return [orderedAvailable[0]];

        var selected = new List<DayOfWeek>(count);
        for (var i = 0; i < count; i++)
        {
            var idx = (int)Math.Floor((double)i * orderedAvailable.Length / count);
            selected.Add(orderedAvailable[Math.Min(idx, orderedAvailable.Length - 1)]);
        }

        return selected.Distinct().ToArray();
    }

    private static DayOfWeek[] OrderWeekdays(IReadOnlySet<DayOfWeek> weekdays) =>
        weekdays.OrderBy(DayOrder).ToArray();

    private static int DayOrder(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;

    private static int ResolveStrengthSessionDurationMinutes(WorkoutGenerationInputV2 input)
    {
        var styleActivity = input.StrengthStyle switch
        {
            StrengthStyleV2.HighVolumeStrength => WorkoutActivityType.HighVolumeStrength,
            StrengthStyleV2.StrengthHighIntensity => WorkoutActivityType.StrengthHighIntensity,
            _ => WorkoutActivityType.GeneralStrength
        };

        if (input.SessionDurationMinutesByActivity.TryGetValue(styleActivity, out var minutes) && minutes > 0)
            return minutes;

        if (input.SessionDurationMinutesByActivity.TryGetValue(WorkoutActivityType.GeneralStrength, out var fallback) && fallback > 0)
            return fallback;

        return 60;
    }

    private static List<PlannedSessionV2> BuildStrengthSessions(
        WorkoutGenerationInputV2 input,
        TrainingExperienceLevel experience,
        StrengthStylePrescriptionSpec styleSpec,
        ExerciseCatalogItemV2[] eligibleCatalog,
        DayOfWeek[] strengthWeekdays,
        SplitKindV2 splitKind,
        int budgetMinutes,
        PainRestrictionV2[] activeRestrictions,
        DateTimeOffset nowUtc,
        List<string> limitations,
        List<string> advisories,
        List<string> trace)
    {
        var existingIds = input.ExistingAcceptedPlan?.Exercises.Select(e => e.ExerciseId).ToHashSet() ?? [];
        var priorWeekIds = input.PriorWeekExerciseIds?.ToHashSet() ?? [];
        var isGluteFocus = input.Focus == TrainingFocus.GluteSpecialization;
        var sessionCount = strengthWeekdays.Length;
        var directGluteIds = eligibleCatalog.Where(item => item.MuscleContributions.Any(contribution =>
            contribution.Muscle == MuscleGroupV2.GluteusMaximus && contribution.Kind == MuscleContributionKind.Direct))
            .Select(item => item.ExerciseId).ToHashSet();
        var meaningfulGluteDays = 0;

        var sessions = new List<PlannedSessionV2>(sessionCount);
        var usedCounts = new Dictionary<int, int>();
        var priorSets = (input.PriorObservedSets ?? []).Where(set => set.SetKind == SetKindV2.Working &&
            set.RepsCompleted > 0 && set.LocalDate >= input.AccountLocalDate.AddDays(-WorkoutPolicyV2.RollingLoadWindowDays) &&
            set.LocalDate < input.AccountLocalDate).ToArray();
        var priorDays = input.PreviousWeekStrengthDays ?? priorSets.Select(set => set.LocalDate).ToHashSet();
        var firstDate = DateForWeekdayInCurrentWeek(input.AccountLocalDate, strengthWeekdays[0]);
        var lowerYesterday = priorSets.Any(set => set.LocalDate == firstDate.AddDays(-1) &&
            input.Catalog.Any(item => item.ExerciseId == set.ExerciseId && IsLowerPattern(item.Pattern)));
        var highRollingLoad = priorDays.Count(date => date >= firstDate.AddDays(-WorkoutPolicyV2.RollingLoadWindowDays) &&
            date < firstDate) >= WorkoutPolicyV2.HighRollingStrengthDayCount;
        if (priorDays.Count > 0 || priorSets.Length > 0)
            trace.Add("Rolling7DayLoadAccounted");
        var upperFirst = lowerYesterday || highRollingLoad;


        for (var i = 0; i < sessionCount; i++)
        {
            var weekday = strengthWeekdays[i];
            var localDate = DateForWeekdayInCurrentWeek(input.AccountLocalDate, weekday);
            var alternatingSplit = splitKind is SplitKindV2.UpperLower or SplitKindV2.Hybrid;
            var isUpperDay = alternatingSplit && (i % 2 == (upperFirst ? 0 : 1));
            var isLowerDay = alternatingSplit && !isUpperDay;

            var slots = new List<ExerciseCatalogItemV2>();

            if (!isUpperDay)
            {
                // Gate 57: Dynamic ordering - Hip Thrust leads session 0 if glute-focused, while other sessions can lead with Squat/RDL
                var preferHipThrustFirst = isGluteFocus && (i % 2 == 0);
                if (preferHipThrustFirst)
                {
                    AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                        c => c.Pattern == MovementPattern.HipThrust || (c.IsGluteDominant && c.Pattern == MovementPattern.HipHinge),
                        preferMachine: true);
                }

                // Knee-dominant (Gate 49)
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => c.Pattern is MovementPattern.SquatPattern or MovementPattern.Lunge,
                    preferMachine: true);

                // Knee flexion (Gate 49)
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => c.Pattern == MovementPattern.KneeFlexion,
                    preferMachine: true);

                // Hip extension (if not already covered by HipThrust/HipHinge above)
                if (!preferHipThrustFirst || !slots.Any(s => s.Pattern is MovementPattern.HipHinge or MovementPattern.HipThrust))
                {
                    var prioritizeDirect = meaningfulGluteDays < WorkoutPolicyV2.TargetDirectGluteDays &&
                        !slots.Any(item => directGluteIds.Contains(item.ExerciseId)) &&
                        eligibleCatalog.Any(item => item.Pattern is (MovementPattern.HipHinge or MovementPattern.HipThrust) &&
                            directGluteIds.Contains(item.ExerciseId));
                    AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                        c => c.Pattern is (MovementPattern.HipHinge or MovementPattern.HipThrust) &&
                            (!prioritizeDirect || directGluteIds.Contains(c.ExerciseId)),
                        preferMachine: true);
                }

                // Gate 56: Direct hip abduction for glute focus
                if (isGluteFocus)
                {
                    AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                        c => c.Pattern == MovementPattern.HipAbduction,
                        preferMachine: true);
                }
            }

            if (!isLowerDay)
            {
                // Upper push (2-week variety: horizontal / incline / fly)
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => c.Pattern is MovementPattern.HorizontalPush or MovementPattern.VerticalPush,
                    preferMachine: true,
                    varietyIndex: i);

                // Upper pull (2-week variety: vertical / horizontal)
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => c.Pattern is MovementPattern.VerticalPull or MovementPattern.HorizontalPull,
                    preferMachine: true,
                    varietyIndex: i);

                // Gate 51: Direct rear delt (weekly mandatory when feasible)
                if (i == 0 || splitKind != SplitKindV2.UpperLower || isUpperDay)
                {
                    AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                        c => c.Pattern == MovementPattern.RearDelt,
                        preferMachine: true);
                }

                // Lateral raise
                if (i > 0 || sessionCount == 1)
                {
                    AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                        c => c.Pattern == MovementPattern.LateralRaise,
                        preferMachine: true);
                }

                // Direct Biceps & Triceps
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => c.Pattern == MovementPattern.ElbowFlexion,
                    preferMachine: true);
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => c.Pattern == MovementPattern.ElbowExtension,
                    preferMachine: true);
            }

            // Gate 50: Direct Calves (>= 2 sets weekly)
            if (!isUpperDay && (i == 0 || i == sessionCount - 1))
            {
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => c.Pattern == MovementPattern.CalfRaise,
                    preferMachine: true);
            }

            // Gate 52: Dynamic Core (target 2 occasions weekly). Every lower day gets its
            // own trunk slot; without it a split lower session drops to three exercises while
            // the paired upper day carries six.
            if (!isUpperDay && (alternatingSplit || i < 2 || sessionCount == 1))
            {
                AddBestCandidate(slots, eligibleCatalog, usedCounts, existingIds, priorWeekIds,
                    c => (c.VarietyTag == ExerciseVarietyTag.DynamicCore ||
                          c.Pattern is MovementPattern.AntiExtension or MovementPattern.AntiRotation or MovementPattern.AntiLateralFlexion) &&
                         c.VarietyTag != ExerciseVarietyTag.StaticCore,
                    preferMachine: true);
            }

            var prescribed = BuildExercisePrescriptions(
                slots, styleSpec, input, activeRestrictions, nowUtc, trace);

            if (i == 0 && upperFirst && !alternatingSplit)
            {
                prescribed = prescribed.Select(exercise => IsLowerPattern(exercise.Pattern)
                    ? exercise with { WorkingSets = 1, EstimatedMinutes = EstimateSingleExerciseMinutes(
                        eligibleCatalog.Single(item => item.ExerciseId == exercise.ExerciseId), exercise.WarmUpSets,
                        1, exercise.MaxRepetitions, exercise.RestSeconds, 1m) }
                    : exercise).ToList();
                trace.Add("Rolling7DayInitialLowerVolumeCapped");
            }

            // Apply 7-step duration optimization ladder (Gate 63, 76, §11)
            var optimized = OptimizeSessionToFitBudget(
                prescribed, eligibleCatalog, styleSpec, budgetMinutes, input.Focus, limitations, advisories, trace, out var totalMinutes);

            if (optimized.Where(exercise => directGluteIds.Contains(exercise.ExerciseId)).Sum(exercise => exercise.WorkingSets)
                >= WorkoutPolicyV2.MeaningfulExposureMinimumDirectSets)
                meaningfulGluteDays++;

            sessions.Add(new PlannedSessionV2(
                SessionIndex: i,
                Weekday: weekday,
                LocalDate: localDate,
                Activity: input.StrengthStyle switch
                {
                    StrengthStyleV2.HighVolumeStrength => WorkoutActivityType.HighVolumeStrength,
                    StrengthStyleV2.StrengthHighIntensity => WorkoutActivityType.StrengthHighIntensity,
                    _ => WorkoutActivityType.GeneralStrength
                },
                SplitKind: splitKind,
                GeneralWarmupMinutes: WorkoutPolicyV2.DefaultGeneralWarmupMinutes,
                EstimatedTotalMinutes: totalMinutes,
                PhysicalWorkingSets: optimized.Sum(e => e.WorkingSets),
                Exercises: optimized));
        }

        AllocateWeeklyDirectVolume(sessions, eligibleCatalog, experience, input.Focus, budgetMinutes, limitations, trace,
            allowIncreases: !input.IsReturningAfterDetraining && !upperFirst);
        return sessions;
    }

    private static bool IsLowerPattern(MovementPattern pattern) => pattern is MovementPattern.SquatPattern
        or MovementPattern.Lunge or MovementPattern.HipHinge or MovementPattern.HipThrust
        or MovementPattern.KneeFlexion or MovementPattern.HipAbduction;

    private static void AllocateWeeklyDirectVolume(List<PlannedSessionV2> sessions,
        ExerciseCatalogItemV2[] catalog, TrainingExperienceLevel experience, TrainingFocus focus,
        int budgetMinutes, List<string> limitations, List<string> trace, bool allowIncreases)
    {
        if (experience == TrainingExperienceLevel.Beginner)
            return;
        var byId = catalog.ToDictionary(item => item.ExerciseId);
        foreach (var muscle in Enum.GetValues<MuscleGroupV2>())
        {
            var target = WorkoutPolicyV2.GetWeeklyVolumeTarget(muscle, experience, focus);
            decimal DirectTotal() => sessions.SelectMany(session => session.Exercises).Sum(exercise =>
                exercise.WorkingSets * byId[exercise.ExerciseId].MuscleContributions.Where(contribution =>
                    contribution.Muscle == muscle && contribution.Kind == MuscleContributionKind.Direct)
                    .Sum(contribution => contribution.CreditPerWorkingSet));
            while (target.MaximumSets > 0 && DirectTotal() > target.MaximumSets)
            {
                var candidate = sessions.SelectMany((session, sessionIndex) => session.Exercises
                        .Select((exercise, exerciseIndex) => (session, sessionIndex, exercise, exerciseIndex)))
                    .Where(candidate => candidate.exercise.WorkingSets > WorkoutPolicyV2.MinimumRoutineWorkingSetsPerExercise &&
                        byId[candidate.exercise.ExerciseId].MuscleContributions.Any(contribution =>
                            contribution.Muscle == muscle && contribution.Kind == MuscleContributionKind.Direct))
                    .OrderByDescending(candidate => candidate.exercise.WorkingSets)
                    .ThenByDescending(candidate => candidate.sessionIndex).FirstOrDefault();
                if (candidate.exercise is null)
                {
                    var removable = sessions.SelectMany((session, sessionIndex) => session.Exercises
                            .Select((exercise, exerciseIndex) => (session, sessionIndex, exercise, exerciseIndex)))
                        .Where(candidate => !candidate.exercise.IsPriorityOrCompound && candidate.exercise.SupersetGroupId is null &&
                            byId[candidate.exercise.ExerciseId].MuscleContributions.Any(contribution =>
                                contribution.Muscle == muscle && contribution.Kind == MuscleContributionKind.Direct &&
                                DirectTotal() - candidate.exercise.WorkingSets * contribution.CreditPerWorkingSet >= target.MinimumSets))
                        .OrderByDescending(candidate => candidate.sessionIndex).FirstOrDefault();
                    if (removable.exercise is null)
                    {
                        limitations.Add($"WeeklyDirectVolumeAboveTarget:{muscle}");
                        break;
                    }
                    var remaining = removable.session.Exercises.Where((_, index) => index != removable.exerciseIndex)
                        .Select((exercise, index) => exercise with { OrderIndex = index }).ToArray();
                    sessions[removable.sessionIndex] = removable.session with { Exercises = remaining,
                        PhysicalWorkingSets = remaining.Sum(exercise => exercise.WorkingSets),
                        EstimatedTotalMinutes = CalculateSessionMinutes(remaining) };
                    trace.Add($"WeeklyDirectAccessoryOccurrenceRemoved:{muscle}:{removable.exercise.ExerciseId}");
                    continue;
                }
                var exercise = candidate.exercise;
                var exercises = candidate.session.Exercises.ToArray();
                var minutes = EstimateSingleExerciseMinutes(byId[exercise.ExerciseId], exercise.WarmUpSets,
                    exercise.WorkingSets - 1, exercise.MaxRepetitions, exercise.RestSeconds, 1m);
                if (exercise.SupersetGroupId is not null) minutes = Math.Round(minutes * SupersetRestSavingFactor, 2);
                exercises[candidate.exerciseIndex] = exercise with { WorkingSets = exercise.WorkingSets - 1,
                    EstimatedMinutes = minutes };
                sessions[candidate.sessionIndex] = candidate.session with { Exercises = exercises,
                    PhysicalWorkingSets = exercises.Sum(item => item.WorkingSets),
                    EstimatedTotalMinutes = CalculateSessionMinutes(exercises) };
                trace.Add($"WeeklyDirectVolumeCapped:{muscle}:{exercise.ExerciseId}");
            }
            if (!allowIncreases) continue;
            // Unavailable equipment coverage (e.g., upper-body pull on bodyweight-only) is disclosed honestly via
            // CheckCoverageGaps rather than blocking feasible strength plans as volume-limited.
            if (!catalog.Any(item => item.MuscleContributions.Any(contribution =>
                    contribution.Muscle == muscle && contribution.Kind == MuscleContributionKind.Direct)))
                continue;
            while (DirectTotal() < target.MinimumSets)
            {
                var candidates = sessions.SelectMany((session, sessionIndex) => session.Exercises
                    .Select((exercise, exerciseIndex) => (session, sessionIndex, exercise, exerciseIndex)))
                    .Where(candidate => candidate.exercise.WorkingSets < WorkoutPolicyV2.MaximumWorkingSetsPerExercise &&
                        byId[candidate.exercise.ExerciseId].MuscleContributions.Any(contribution =>
                            contribution.Muscle == muscle && contribution.Kind == MuscleContributionKind.Direct))
                    .OrderBy(candidate => candidate.exercise.WorkingSets).ThenBy(candidate => candidate.sessionIndex)
                    .ThenBy(candidate => candidate.exerciseIndex).ToArray();
                var added = false;
                foreach (var candidate in candidates)
                {
                    var exercise = candidate.exercise;
                    var minutes = EstimateSingleExerciseMinutes(byId[exercise.ExerciseId], exercise.WarmUpSets,
                        exercise.WorkingSets + 1, exercise.MaxRepetitions, exercise.RestSeconds, 1m);
                    if (candidate.session.EstimatedTotalMinutes - exercise.EstimatedMinutes + minutes > budgetMinutes)
                        continue;
                    var exercises = candidate.session.Exercises.ToArray();
                    exercises[candidate.exerciseIndex] = exercise with { WorkingSets = exercise.WorkingSets + 1,
                        EstimatedMinutes = minutes };
                    sessions[candidate.sessionIndex] = candidate.session with { Exercises = exercises,
                        PhysicalWorkingSets = exercises.Sum(item => item.WorkingSets),
                        EstimatedTotalMinutes = CalculateSessionMinutes(exercises) };
                    added = true;
                    trace.Add($"WeeklyDirectSetAdded:{muscle}:{exercise.ExerciseId}");
                    break;
                }
                if (added) continue;
                limitations.Add($"WeeklyDirectVolumeBelowTarget:{muscle}");
                break;
            }
        }
    }

    private static void AddBestCandidate(
        List<ExerciseCatalogItemV2> sessionSlots,
        ExerciseCatalogItemV2[] eligibleCatalog,
        Dictionary<int, int> usedCounts,
        HashSet<int> existingIds,
        HashSet<int> priorWeekIds,
        Func<ExerciseCatalogItemV2, bool> predicate,
        bool preferMachine,
        int varietyIndex = 0)
    {
        var sessionIds = sessionSlots.Select(s => s.ExerciseId).ToHashSet();
        var candidate = eligibleCatalog
            .Where(c => !sessionIds.Contains(c.ExerciseId) && predicate(c))
            .OrderBy(c => usedCounts.GetValueOrDefault(c.ExerciseId, 0))
            .ThenByDescending(c => existingIds.Contains(c.ExerciseId))
            .ThenByDescending(c => preferMachine && c.PrefersMachineOrCable)
            .ThenBy(c => priorWeekIds.Contains(c.ExerciseId) && varietyIndex > 0 ? 1 : 0)
            .ThenBy(c => c.ExerciseId)
            .FirstOrDefault();

        if (candidate is null)
            return;

        sessionSlots.Add(candidate);
        usedCounts[candidate.ExerciseId] = usedCounts.GetValueOrDefault(candidate.ExerciseId, 0) + 1;
    }

    private static List<PlannedExercisePrescriptionV2> BuildExercisePrescriptions(
        List<ExerciseCatalogItemV2> slots,
        StrengthStylePrescriptionSpec styleSpec,
        WorkoutGenerationInputV2 input,
        PainRestrictionV2[] activeRestrictions,
        DateTimeOffset nowUtc,
        List<string> trace)
    {
        var list = new List<PlannedExercisePrescriptionV2>(slots.Count);
        var recoveryIsAdequate = !HasPoorReadiness(input.DailyReadiness);
        for (var idx = 0; idx < slots.Count; idx++)
        {
            var item = slots[idx];
            var isPriority = item.IsCompound || (input.Focus == TrainingFocus.GluteSpecialization && item.IsGluteDominant);
            var workingSets = styleSpec.DefaultWorkingSets;
            var minReps = item.IsCompound ? styleSpec.CompoundMinReps : styleSpec.IsolationMinReps;
            var maxReps = item.IsCompound ? styleSpec.CompoundMaxReps : styleSpec.IsolationMaxReps;
            var restSeconds = item.IsCompound ? styleSpec.CompoundRestSeconds : styleSpec.IsolationRestSeconds;
            var warmupSets = item.IsCompound ? 1 : 0;

            // Gates 71-75, 77 & 88: Load and progression prescription from actual history, conservative return, or calibration
            var (loadStatus, prescribedLoad, reason, adjustedWorkingSets) = ResolveInitialLoadAndProgression(
                item, workingSets, minReps, maxReps, styleSpec.TargetRir, input, recoveryIsAdequate, nowUtc, trace);
            workingSets = adjustedWorkingSets;

            var estMinutes = EstimateSingleExerciseMinutes(item, warmupSets, workingSets, maxReps, restSeconds, supersetFactor: 1.0m);

            list.Add(new PlannedExercisePrescriptionV2(
                OrderIndex: idx,
                ExerciseId: item.ExerciseId,
                ExerciseName: item.Name,
                Pattern: item.Pattern,
                IsPriorityOrCompound: isPriority,
                WarmUpSets: warmupSets,
                WorkingSets: workingSets,
                MinRepetitions: minReps,
                MaxRepetitions: maxReps,
                TargetRir: styleSpec.TargetRir,
                RestSeconds: restSeconds,
                LoadStatus: loadStatus,
                PrescribedLoadKg: prescribedLoad,
                EstimatedMinutes: estMinutes,
                SupersetGroupId: null,
                SelectionReasonCode: reason));
        }

        return list;
    }

    private static (SessionLoadStatusV2 Status, decimal? LoadKg, string Reason, int WorkingSets) ResolveInitialLoadAndProgression(
        ExerciseCatalogItemV2 item,
        int workingSets,
        int minReps,
        int maxReps,
        int targetRir,
        WorkoutGenerationInputV2 input,
        bool recoveryIsAdequate,
        DateTimeOffset nowUtc,
        List<string> trace)
    {
        if (!item.IsBodyweightOnly &&
            (input.ActivePainRestrictions ?? []).Any(restriction => restriction.IsResolved &&
            restriction.RequiresReturnCalibration &&
            restriction.ConservativeReturnSessionsCompleted < WorkoutPolicyV2.ConservativeReturnSessionCount &&
            (restriction.ExerciseId == item.ExerciseId || restriction.BlockedPattern == item.Pattern)))
            return (SessionLoadStatusV2.CalibrationRequired, null, "InitialLoadCalibrationRequired", workingSets);
        var priorSets = (input.PriorObservedSets ?? [])
            .Where(s => s.ExerciseId == item.ExerciseId && s.SetKind == SetKindV2.Working && s.LoadKg > 0m)
            .OrderByDescending(s => s.CompletedAtUtc)
            .ToArray();

        if (priorSets.Length == 0)
        {
            return item.IsBodyweightOnly
                ? (SessionLoadStatusV2.BodyweightZeroAddedLoad, 0m, "BodyweightZeroAddedLoad", workingSets)
                : (SessionLoadStatusV2.CalibrationRequired, null, "InitialLoadCalibrationRequired", workingSets);
        }

        var latestLoad = priorSets[0].LoadKg;
        var resolvedRestriction = (input.ActivePainRestrictions ?? [])
            .FirstOrDefault(r => r.IsResolved &&
                                 (r.ExerciseId == item.ExerciseId || r.BlockedPattern == item.Pattern) &&
                                 r.ConservativeReturnSessionsCompleted < WorkoutPolicyV2.ConservativeReturnSessionCount);

        if (resolvedRestriction is not null)
        {
            var painReturn = WorkoutProgressionAndRecoveryV2.EvaluatePainReturn(
                resolvedRestriction, nowUtc, resolvedRestriction.ReturnBaselineLoadKg ?? latestLoad, item.MinimumLoadIncrementKg);
            if (painReturn.CanReturnWithConservativeLoad)
            {
                return (SessionLoadStatusV2.ConservativeReturnLoad, painReturn.ConservativeLoadKg, painReturn.ReasonCode, workingSets);
            }
        }

        var progression = WorkoutProgressionAndRecoveryV2.EvaluateStrengthProgression(
            item,
            workingSets,
            minReps,
            maxReps,
            targetRir,
            priorSets,
            input.Focus,
            recoveryIsAdequate);

        switch (progression.Decision)
        {
            case ProgressionDecisionKindV2.RecommendIncrease when progression.RecommendedLoadKg is > 0m:
                trace.Add($"StrengthProgression:{item.ExerciseId}:{progression.ReasonCode}:{progression.RecommendedLoadKg.Value.ToString(CultureInfo.InvariantCulture)}");
                return (SessionLoadStatusV2.PrescribedLoad, progression.RecommendedLoadKg, progression.ReasonCode, workingSets);

            case ProgressionDecisionKindV2.RecommendDecrease when progression.RecommendedLoadKg is >= 0m:
                trace.Add($"StrengthProgression:{item.ExerciseId}:{progression.ReasonCode}:{progression.RecommendedLoadKg.Value.ToString(CultureInfo.InvariantCulture)}");
                return (SessionLoadStatusV2.PrescribedLoad, progression.RecommendedLoadKg, progression.ReasonCode, workingSets);

            case ProgressionDecisionKindV2.RecommendGlutePlateauPlusOneSet:
                var boostedSets = Math.Min(
                    WorkoutPolicyV2.MaximumWorkingSetsPerExercise,
                    workingSets + (progression.RecommendedAdditionalWeeklySets ?? 1));
                trace.Add($"GlutePlateauSetAdded:{item.ExerciseId}:{boostedSets}");
                return (SessionLoadStatusV2.PrescribedLoad, progression.RecommendedLoadKg ?? latestLoad, progression.ReasonCode, boostedSets);

            default:
                var holdReason = progression.ReasonCode is
                    WorkoutProgressionAndRecoveryV2.SingleDeficitHoldsLoadCode or
                    WorkoutProgressionAndRecoveryV2.TechniqueBlockedIncreaseCode
                    ? progression.ReasonCode
                    : "PriorComparableHistory";
                return (SessionLoadStatusV2.PrescribedLoad, progression.RecommendedLoadKg ?? latestLoad, holdReason, workingSets);
        }
    }

    private static List<PlannedExercisePrescriptionV2> OptimizeSessionToFitBudget(
        List<PlannedExercisePrescriptionV2> exercises,
        ExerciseCatalogItemV2[] eligibleCatalog,
        StrengthStylePrescriptionSpec styleSpec,
        int budgetMinutes,
        TrainingFocus focus,
        List<string> limitations,
        List<string> advisories,
        List<string> trace,
        out decimal totalMinutes)
    {
        var catalogById = eligibleCatalog.ToDictionary(c => c.ExerciseId);
        totalMinutes = CalculateSessionMinutes(exercises);
        if (totalMinutes <= budgetMinutes)
            return exercises;

        // Step 3 of optimization ladder: Create compatible supersets (excluding heavy HI compounds)
        if (styleSpec.Style != StrengthStyleV2.StrengthHighIntensity)
        {
            exercises = ApplyCompatibleSupersets(exercises, catalogById, trace);
            totalMinutes = CalculateSessionMinutes(exercises);
            if (totalMinutes <= budgetMinutes)
            {
                advisories.Add("CompatibleSupersetsAppliedForDuration");
                return exercises;
            }
        }

        // Step 5: Remove lower-priority accessories while protecting priority/compound and glute focus work
        var working = exercises.ToList();
        while (totalMinutes > budgetMinutes && working.Count > 1)
        {
            var removable = working
                .Select((ex, idx) => new { ex, idx })
                .Reverse()
                .FirstOrDefault(x => !x.ex.IsPriorityOrCompound &&
                                     !(focus == TrainingFocus.GluteSpecialization &&
                                       catalogById.TryGetValue(x.ex.ExerciseId, out var c) && c.IsGluteDominant));

            if (removable is null)
                break;

            working.RemoveAt(removable.idx);
            totalMinutes = CalculateSessionMinutes(working);
            limitations.Add(PartialCoverageTimeLimitedCode);
            trace.Add($"TrimmedAccessory:{removable.ex.ExerciseId}");
        }

        // A short session must not collapse to two exercises and lose an entire movement region.
        // Before trimming priority work, reduce working sets (never below two) so a four-exercise
        // full-body floor can fit the user's budget.
        if (totalMinutes > budgetMinutes && working.Count >= 4)
        {
            var reducedSets = false;
            var candidates = working
                .Select((exercise, index) => new { exercise, index })
                .Where(candidate => candidate.exercise.WorkingSets > 2)
                .Reverse()
                .ToArray();
            foreach (var candidate in candidates)
            {
                var sets = candidate.exercise.WorkingSets;
                while (sets > 2 && totalMinutes > budgetMinutes)
                {
                    sets--;
                    var catalogItem = catalogById[candidate.exercise.ExerciseId];
                    var minutes = EstimateSingleExerciseMinutes(catalogItem,
                        candidate.exercise.WarmUpSets, sets, candidate.exercise.MaxRepetitions,
                        candidate.exercise.RestSeconds,
                        candidate.exercise.SupersetGroupId is null ? 1m : SupersetRestSavingFactor);
                    working[candidate.index] = candidate.exercise with
                    {
                        WorkingSets = sets,
                        EstimatedMinutes = minutes
                    };
                    totalMinutes = CalculateSessionMinutes(working);
                    trace.Add($"TrimmedSets:{candidate.exercise.ExerciseId}:{sets}");
                    reducedSets = true;
                }
                if (totalMinutes <= budgetMinutes)
                    break;
            }
            if (reducedSets)
                limitations.Add(PartialCoverageTimeLimitedCode);
            if (totalMinutes <= budgetMinutes)
                return working;
        }

        // Step 6-7: If still over budget, trim lowest-priority remaining exercise while preserving >= 1 safe core exercise
        while (totalMinutes > budgetMinutes && working.Count > 1)
        {
            var removed = working[^1];
            working.RemoveAt(working.Count - 1);
            totalMinutes = CalculateSessionMinutes(working);
            limitations.Add(PartialCoverageTimeLimitedCode);
            trace.Add($"TrimmedExercise:{removed.ExerciseId}");
        }

        // Re-index order
        for (var i = 0; i < working.Count; i++)
            working[i] = working[i] with { OrderIndex = i };

        return working;
    }

    private static List<PlannedExercisePrescriptionV2> ApplyCompatibleSupersets(
        List<PlannedExercisePrescriptionV2> exercises,
        Dictionary<int, ExerciseCatalogItemV2> catalogById,
        List<string> trace)
    {
        var result = exercises.ToList();
        var nextGroupId = 1;
        var paired = new HashSet<int>();

        for (var i = 0; i < result.Count; i++)
        {
            if (paired.Contains(i))
                continue;

            var first = result[i];
            if (first.IsPriorityOrCompound)
                continue;

            for (var j = i + 1; j < result.Count; j++)
            {
                if (paired.Contains(j))
                    continue;

                var second = result[j];
                if (second.IsPriorityOrCompound)
                    continue;

                if (AreSupersetCompatible(first, second, catalogById))
                {
                    var groupId = nextGroupId++;
                    paired.Add(i);
                    paired.Add(j);
                    result[i] = first with
                    {
                        SupersetGroupId = groupId,
                        EstimatedMinutes = Math.Round(first.EstimatedMinutes * SupersetRestSavingFactor, 2)
                    };
                    result[j] = second with
                    {
                        SupersetGroupId = groupId,
                        EstimatedMinutes = Math.Round(second.EstimatedMinutes * SupersetRestSavingFactor, 2)
                    };
                    trace.Add($"SupersetPaired:{first.ExerciseId}+{second.ExerciseId}");
                    break;
                }
            }
        }

        return result;
    }

    private static bool AreSupersetCompatible(
        PlannedExercisePrescriptionV2 a,
        PlannedExercisePrescriptionV2 b,
        Dictionary<int, ExerciseCatalogItemV2> catalogById)
    {
        if (!catalogById.TryGetValue(a.ExerciseId, out var itemA) ||
            !catalogById.TryGetValue(b.ExerciseId, out var itemB))
            return false;

        // Antagonist or non-overlapping primary muscles
        var directA = itemA.MuscleContributions
            .Where(c => c.Kind == MuscleContributionKind.Direct)
            .Select(c => c.Muscle)
            .ToHashSet();
        var directB = itemB.MuscleContributions
            .Where(c => c.Kind == MuscleContributionKind.Direct)
            .Select(c => c.Muscle)
            .ToHashSet();

        return !directA.Overlaps(directB);
    }

    private static decimal CalculateSessionMinutes(IReadOnlyList<PlannedExercisePrescriptionV2> exercises) =>
        WorkoutPolicyV2.DefaultGeneralWarmupMinutes + exercises.Sum(e => e.EstimatedMinutes);

    private static decimal EstimateSingleExerciseMinutes(
        ExerciseCatalogItemV2 item,
        int warmupSets,
        int workingSets,
        int maxReps,
        int restSeconds,
        decimal supersetFactor)
    {
        var unilateralMultiplier = item.IsUnilateral ? 2m : 1m;
        var executionMinutes = (decimal)workingSets * maxReps * item.SecondsPerRep * unilateralMultiplier / SecondsPerMinute;
        var interSetRestMinutes = workingSets > 1
            ? (decimal)(workingSets - 1) * restSeconds / SecondsPerMinute * supersetFactor
            : 0m;
        var warmupMinutes = warmupSets * WarmupSetDurationMinutes;
        return Math.Round(item.SetupMinutes + warmupMinutes + executionMinutes + interSetRestMinutes, 2);
    }

    private static List<PlannedSessionV2> ScheduleSelectedCardio(
        WorkoutGenerationInputV2 input,
        List<PlannedSessionV2> strengthSessions,
        DayOfWeek[] orderedAvailableWeekdays,
        List<string> advisories,
        List<string> trace)
    {
        var selectedCardio = input.SelectedActivities
            .Where(WorkoutPolicyV2.IsCardioActivitySupportedInV2)
            .OrderBy(a => (int)a)
            .ToArray();

        if (selectedCardio.Length == 0)
            return strengthSessions;

        var result = strengthSessions.ToList();
        var strengthDays = result.Select(s => s.Weekday).ToHashSet();
        var nonStrengthDays = orderedAvailableWeekdays.Where(d => !strengthDays.Contains(d)).ToArray();

        foreach (var cardio in selectedCardio)
        {
            var cardioProgression = WorkoutProgressionAndRecoveryV2.EvaluateCardioProgression(cardio, input.PriorCardioActuals);
            var hasExplicitDuration = input.SessionDurationMinutesByActivity.TryGetValue(cardio, out var m) && m > 0;
            var requestedMinutes = hasExplicitDuration
                ? (cardioProgression.ReasonCode == "SingleVariableDurationStepRecommended" &&
                   cardioProgression.RecommendedDurationMinutes is { } stepMin && stepMin > 0
                    ? Math.Min(m, stepMin)
                    : m)
                : (cardioProgression.RecommendedDurationMinutes ?? 25);
            if (cardioProgression.ReasonCode == "SingleVariableDurationStepRecommended")
                trace.Add($"CardioProgression:{cardio}:{cardioProgression.ReasonCode}:{requestedMinutes}m");

            if (nonStrengthDays.Length > 0)
            {
                // Place independent cardio on a non-strength day (up to 2 independent sessions/day)
                var targetDay = nonStrengthDays[0];
                var sessionsOnDay = result.Count(s => s.Weekday == targetDay);
                if (sessionsOnDay < WorkoutPolicyV2.MaximumIndependentSessionsPerDay)
                {
                    result.Add(new PlannedSessionV2(
                        SessionIndex: result.Count,
                        Weekday: targetDay,
                        LocalDate: DateForWeekdayInCurrentWeek(input.AccountLocalDate, targetDay),
                        Activity: cardio,
                        SplitKind: null,
                        GeneralWarmupMinutes: 5,
                        EstimatedTotalMinutes: requestedMinutes,
                        PhysicalWorkingSets: 0,
                        Exercises: [],
                        CardioIntensity: CardioIntensityBandV2.Moderate));
                    trace.Add($"CardioScheduledStandalone:{cardio}:{targetDay}");
                    continue;
                }
            }

            // Otherwise attach as a light post-strength finisher <= 30 min (Gate 78)
            if (result.Count > 0)
            {
                var firstStrength = result[0];
                if (firstStrength.AttachedFinisher is null)
                {
                    var finisherMinutes = Math.Min(requestedMinutes, WorkoutPolicyV2.MaximumPostStrengthFinisherMinutes);
                    result[0] = firstStrength with
                    {
                        EstimatedTotalMinutes = firstStrength.EstimatedTotalMinutes + finisherMinutes,
                        AttachedFinisher = new CardioFinisherPrescriptionV2(
                            cardio, finisherMinutes, CardioIntensityBandV2.Light, 2, 3)
                    };
                    advisories.Add("CardioAttachedAsPostStrengthFinisher");
                    trace.Add($"CardioFinisherAttached:{cardio}:{finisherMinutes}m");
                }
            }
        }

        return result;
    }

    private static bool HasPoorReadiness(DailyReadinessInputV2? readiness)
    {
        if (readiness is null)
            return false;

        // Gate 89: SorenessDoms NEVER affects readiness dosing.
        // Gate 84: CycleContext phase alone NEVER affects readiness dosing.
        var lowEnergy = readiness.EnergyLevel is <= 1;
        var highFatigue = readiness.FatigueLevel is >= 3;
        var poorSleep = readiness.SleepQuality is <= 1 || readiness.SleepHours is < 6.5m;
        var significantCycleSymptoms = readiness.HasSignificantMenstrualSymptoms;

        return lowEnergy || highFatigue || poorSleep || significantCycleSymptoms;
    }

    private static (List<PlannedSessionV2> Adapted, bool Applied, string? Explanation) ApplyReadinessAdaptation(
        List<PlannedSessionV2> originalSessions,
        WorkoutGenerationInputV2 input,
        Dictionary<int, ExerciseCatalogItemV2> catalogById,
        List<string> trace)
    {
        if (!HasPoorReadiness(input.DailyReadiness))
            return (originalSessions, false, null);

        // Gate 90: Reduce 1 optional accessory working set on the first strength session; keep main exercises & weights intact
        var adapted = new List<PlannedSessionV2>(originalSessions.Count);
        var reducedAny = false;

        foreach (var session in originalSessions)
        {
            if (!reducedAny && session.Exercises.Count > 0)
            {
                var exList = session.Exercises.ToList();
                var targetIdx = exList.FindLastIndex(e => !e.IsPriorityOrCompound && e.WorkingSets > 1);
                if (targetIdx >= 0)
                {
                    var target = exList[targetIdx];
                    exList[targetIdx] = target with { WorkingSets = target.WorkingSets - 1 };
                    reducedAny = true;
                    trace.Add($"ReadinessReducedOptionalSet:{target.ExerciseId}");
                    adapted.Add(session with
                    {
                        Exercises = exList,
                        PhysicalWorkingSets = exList.Sum(e => e.WorkingSets)
                    });
                    continue;
                }
            }

            adapted.Add(session);
        }

        return reducedAny
            ? (adapted, true, ReadinessOptionalSetReducedExplanation)
            : (originalSessions, false, null);
    }

    private static void CheckCoverageGaps(
        MuscleAccountingSummaryV2 summary,
        WorkoutGenerationInputV2 input,
        IReadOnlyList<PlannedSessionV2> sessions,
        List<string> limitations,
        List<string> advisories)
    {
        if (!summary.CoversKneeDominant) advisories.Add(WeeklyMovementCoverageRule.MissingKneeExtension);
        if (!summary.CoversKneeFlexion) advisories.Add(WeeklyMovementCoverageRule.MissingKneeFlexion);
        if (!summary.CoversHipExtension) advisories.Add(WeeklyMovementCoverageRule.MissingHipExtension);
        if (!sessions.SelectMany(session => session.Exercises).Any(exercise =>
                exercise.Pattern is MovementPattern.HorizontalPull or MovementPattern.VerticalPull))
        {
            var hasBandsWithoutAnchor = input.AvailableEquipment.Contains(ExerciseEquipmentKind.ResistanceBand) &&
                !input.AvailableEquipment.Contains(ExerciseEquipmentKind.BandAnchor);
            advisories.Add(hasBandsWithoutAnchor
                ? "WeeklyCoverageMissing_UpperPull_BandAnchorRequired"
                : "WeeklyCoverageMissing_UpperPull_Equipment");
        }
        if (summary.Muscles.Any(muscle => muscle.Muscle == MuscleGroupV2.GluteusMaximus &&
                muscle.MeaningfulDirectDays < WorkoutPolicyV2.TargetDirectGluteDays))
            advisories.Add("WeeklyCoverageBelowTarget_DirectGluteDays");
        if (!summary.CoversDirectCalves) advisories.Add("WeeklyCoverageMissing_DirectCalves");
        if (!summary.CoversDirectRearDelts) advisories.Add("WeeklyCoverageMissing_DirectRearDelts");
        if (summary.DynamicCoreOccasions < 2) advisories.Add("WeeklyCoverageBelowTarget_DynamicCore");
        if (input.Focus == TrainingFocus.GluteSpecialization && !summary.CoversDirectHipAbduction)
            advisories.Add("WeeklyCoverageMissing_GluteFocusHipAbduction");
    }

    private static DateOnly DateForWeekdayInCurrentWeek(DateOnly referenceDate, DayOfWeek targetWeekday)
    {
        var currentOrder = DayOrder(referenceDate.DayOfWeek);
        var targetOrder = DayOrder(targetWeekday);
        var delta = (targetOrder - currentOrder + 7) % 7;
        return referenceDate.AddDays(delta);
    }

    private static string ComputeHash(
        IReadOnlyList<PlannedSessionV2> sessions,
        IReadOnlyList<string> limitations,
        IReadOnlyList<string> advisories)
    {
        var sb = new StringBuilder();
        foreach (var s in sessions.OrderBy(x => x.SessionIndex))
        {
            sb.Append(CultureInfo.InvariantCulture, $"S:{s.SessionIndex}:{s.Weekday}:{s.Activity}:{s.SplitKind}|");
            foreach (var e in s.Exercises.OrderBy(x => x.OrderIndex))
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"E:{e.OrderIndex}:{e.ExerciseId}:{e.WarmUpSets}:{e.WorkingSets}:{e.MinRepetitions}-{e.MaxRepetitions}:{e.TargetRir}:{e.RestSeconds}:{e.LoadStatus}:{e.PrescribedLoadKg}:{e.SupersetGroupId}|");
            }
            if (s.AttachedFinisher is { } f)
                sb.Append(CultureInfo.InvariantCulture, $"F:{f.Activity}:{f.DurationMinutes}:{f.IntensityBand}|");
        }
        foreach (var l in limitations) sb.Append("L:").Append(l).Append('|');
        foreach (var a in advisories) sb.Append("A:").Append(a).Append('|');

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static WorkoutGenerationResultV2 InvalidResult(
        WorkoutGenerationStatusV2 status,
        string[] limitations,
        string policyVersion,
        string catalogVersion) =>
        new(status, [], [], null, limitations, [], limitations, policyVersion, catalogVersion,
            ComputeHash([], limitations, []));
}
