using System.Globalization;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum InjurySafetyStatus
{
    Acute,
    Recovering,
    Cleared
}

public enum ProgressionAction
{
    Advance,
    Maintain,
    Regress
}

public static class WeeklyWorkoutPlanGeneratorRule
{
    public const string PolicyId = "MC-WORKOUT-PLANNER-UNIFIED";
    public const string Version = "1.0.0";
    public const string RulesetVersion = "MC-PLANNING-0.2";

    public const int Step1InputAggregation = 1;
    public const int Step2EffectiveExperienceTier = 2;
    public const int Step3StrengthSessionConstruction = 3;
    public const int Step4MixedActivityPackingAndVolume = 4;
    public const int Step5ProgressionAndSafetyGates = 5;
    public const int Step6PhaseCheckInPrecedence = 6;
    public const int Step7PlanFinalizationAndTrace = 7;

    public const int DefaultWarmupMinutes = 5;
    public const int DefaultCooldownMinutes = 5;
    public const int DefaultPreferredSessionLengthMinutes = 45;
    public const int MinSessionMinutes = 15;
    public const int MaxSessionMinutes = 180;
    public const int DefaultGeneralDaysPerWeek = 3;
    public const int MinWeeklyDays = 2;
    public const int MaxWeeklyDays = 7;
    public const int MaxHardDaysPerWeek = 3;
    public const int MaxHardDaysPerDay = 1;

    public const int DefaultCompoundMinReps = 6;
    public const int DefaultCompoundMaxReps = 12;
    public const int DefaultAccessoryMinReps = 10;
    public const int DefaultAccessoryMaxReps = 20;
    public const int DefaultTargetRir = 2;

    public const decimal DefaultSecondsPerRep = 4.0m;
    public const decimal DefaultCompoundRestSeconds = 90.0m;
    public const decimal DefaultAccessoryRestSeconds = 60.0m;
    public const decimal SecondsPerMinute = 60.0m;

    public const int DefaultCompoundSets = 3;
    public const int DefaultAccessorySets = 2;
    public const int MinimumPrescribedSets = 1;

    public const decimal CompoundProgressionIncrementKg = 2.5m;
    public const decimal AccessoryProgressionIncrementKg = 1.25m;

    public static DeterministicWeeklyPlanResult Generate(WeeklyPlanGeneratorInput? input)
    {
        var trace = new List<RuleTraceEntry>();
        var generatedAt = DateTimeOffset.UtcNow;
        var planId = Guid.NewGuid();

        // -------------------------------------------------------------
        // Step 1: Input Aggregation & Validation
        // -------------------------------------------------------------
        var sanitizedInput = SanitizeInput(input);
        trace.Add(new RuleTraceEntry(
            StepNumber: Step1InputAggregation,
            StepName: "InputAggregation",
            DecisionSummary: $"Sanitized profile and scheduling inputs: {sanitizedInput.GeneralDaysPerWeek} days/week, {sanitizedInput.PreferredSessionLengthMinutes} min/session, Goal: {sanitizedInput.Goal}.",
            Details: new Dictionary<string, string>
            {
                ["Goal"] = sanitizedInput.Goal.ToString(),
                ["DaysPerWeek"] = sanitizedInput.GeneralDaysPerWeek.ToString(CultureInfo.InvariantCulture),
                ["SessionLengthMinutes"] = sanitizedInput.PreferredSessionLengthMinutes.ToString(CultureInfo.InvariantCulture),
                ["AvailableWeekdaysCount"] = sanitizedInput.AvailableWeekdays.Count.ToString(CultureInfo.InvariantCulture)
            }));

        // -------------------------------------------------------------
        // Step 2: Compute EffectiveExperienceTier per activity
        // -------------------------------------------------------------
        var effectiveTiers = new Dictionary<WorkoutActivityType, int>();
        var globalTier = 1;
        foreach (var activity in sanitizedInput.PreferredActivities)
        {
            sanitizedInput.ActivityHistories.TryGetValue(activity, out var hist);
            var resolved = EffectiveExperienceTierRule.Resolve(
                sanitizedInput.ProfileExperience,
                activity.ToString(),
                hist,
                sanitizedInput.PlanWeekStartDate);
            effectiveTiers[activity] = (int)resolved.Tier;
            globalTier = resolved.GlobalTier;
        }

        var strengthActivity = DetermineStrengthActivity(sanitizedInput.PreferredActivities, sanitizedInput.StrengthStyle);
        var strengthTier = effectiveTiers.GetValueOrDefault(strengthActivity, globalTier);

        trace.Add(new RuleTraceEntry(
            StepNumber: Step2EffectiveExperienceTier,
            StepName: "EffectiveExperienceTier",
            DecisionSummary: $"Computed effective tiers. Global Tier: {globalTier}, Strength Activity: {strengthActivity} at Tier {strengthTier}.",
            Details: effectiveTiers.ToDictionary(k => k.Key.ToString(), v => v.Value.ToString(CultureInfo.InvariantCulture))));

        // -------------------------------------------------------------
        // Step 3: Build strength sessions, resolve exercises & time fitting
        // -------------------------------------------------------------
        var warmup = sanitizedInput.WarmupMinutes;
        var cooldown = sanitizedInput.CooldownMinutes;
        var workBudgetMinutes = Math.Max(MinSessionMinutes, sanitizedInput.PreferredSessionLengthMinutes - (warmup + cooldown));

        var strengthSessions = ConstructStrengthSessions(
            strengthActivity: strengthActivity,
            strengthTier: strengthTier,
            daysPerWeek: sanitizedInput.GeneralDaysPerWeek,
            availableWeekdays: sanitizedInput.AvailableWeekdays,
            excludedExerciseIds: sanitizedInput.ExcludedExerciseIds,
            workBudgetMinutes: workBudgetMinutes);

        trace.Add(new RuleTraceEntry(
            StepNumber: Step3StrengthSessionConstruction,
            StepName: "StrengthSessionConstruction",
            DecisionSummary: $"Constructed {strengthSessions.Count} strength sessions fitted to {workBudgetMinutes} min work budget ({warmup}m warmup, {cooldown}m cooldown).",
            Details: new Dictionary<string, string>
            {
                ["SessionCount"] = strengthSessions.Count.ToString(CultureInfo.InvariantCulture),
                ["StrengthActivity"] = strengthActivity.ToString(),
                ["WorkBudgetMinutes"] = workBudgetMinutes.ToString(CultureInfo.InvariantCulture)
            }));

        // -------------------------------------------------------------
        // Step 4: Pack mixed activities, volume caps & WHO targets
        // -------------------------------------------------------------
        var (packedSessions, hardDaysCount) = PackMixedActivities(
            strengthSessions: strengthSessions,
            preferredActivities: sanitizedInput.PreferredActivities,
            availableWeekdays: sanitizedInput.AvailableWeekdays,
            effectiveTiers: effectiveTiers,
            preferredDurationMinutes: sanitizedInput.PreferredSessionLengthMinutes);

        // Weekly hypertrophy direct-set volume check
        var plannedExerciseSets = packedSessions
            .SelectMany(s => s.Exercises)
            .Select(e => new PlannedExerciseSets(
                ExerciseId: e.ExerciseId,
                PrimaryMuscle: TryParseMajorMuscle(e.PrimaryMuscle, out var g) ? g : MajorMuscleGroup.Chest,
                Sets: e.TargetSets))
            .ToList();
        var strengthTierEnum = Enum.IsDefined((EffectiveExperienceTier)strengthTier)
            ? (EffectiveExperienceTier)strengthTier
            : EffectiveExperienceTier.Tier1BeginnerOrNew;
        var hypertrophyVolumeResult = WeeklyHypertrophyVolumeRule.Evaluate(strengthTierEnum, plannedExerciseSets);

        // Aerobic minutes check
        var aerobicSessions = packedSessions
            .Where(s => s.Activity != WorkoutActivityType.GeneralStrength &&
                        s.Activity != WorkoutActivityType.HighVolumeStrength &&
                        s.Activity != WorkoutActivityType.StrengthHighIntensity &&
                        s.Activity != WorkoutActivityType.RestDay)
            .Select(s => new AerobicSessionMinutes(
                MinutesModerate: s.IsHardSession ? 0m : s.EstimatedMinutes,
                MinutesVigorous: s.IsHardSession ? s.EstimatedMinutes : 0m))
            .ToList();

        var resistanceDaysCount = packedSessions.Count(s =>
            s.Activity is WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.StrengthHighIntensity);

        var aerobicHealthSummary = AerobicHealthTargetRule.Evaluate(aerobicSessions, resistanceDaysCount);

        trace.Add(new RuleTraceEntry(
            StepNumber: Step4MixedActivityPackingAndVolume,
            StepName: "MixedActivityPackingAndVolume",
            DecisionSummary: $"Packed {packedSessions.Count} weekly sessions with {hardDaysCount} hard days. WHO target met: {aerobicHealthSummary.MeetsAllTargets}.",
            Details: new Dictionary<string, string>
            {
                ["TotalSessions"] = packedSessions.Count.ToString(CultureInfo.InvariantCulture),
                ["HardDaysCount"] = hardDaysCount.ToString(CultureInfo.InvariantCulture),
                ["WeeklyModerateEquivalentMinutes"] = aerobicHealthSummary.TotalModerateEquivalentMinutes.ToString(CultureInfo.InvariantCulture),
                ["MeetsWhoTarget"] = aerobicHealthSummary.MeetsAllTargets.ToString(),
                ["HypertrophyAllMusclesSatisfied"] = hypertrophyVolumeResult.AllTargetedMusclesWithinBounds.ToString()
            }));

        // -------------------------------------------------------------
        // Step 5: Progression, Injury safety gates & Deload checks
        // -------------------------------------------------------------
        var deloadPrescription = DeloadTriggerRule.Evaluate(sanitizedInput.RecoveryHistory);
        var hasInjuryBlock = false;

        // Apply deload adjustments or double progression
        var finalSessions = new List<DeterministicPlannedSession>(packedSessions.Count);
        foreach (var session in packedSessions)
        {
            var updatedExercises = new List<PlannedExercisePrescription>(session.Exercises.Count);
            foreach (var ex in session.Exercises)
            {
                var targetSets = ex.TargetSets;
                var targetRir = ex.TargetRir;
                var targetLoad = ex.PrescribedLoadKg;
                var progressionAction = ProgressionAction.Maintain;
                var rationale = "Standard baseline volume prescription.";

                // Deload adjustment
                if (deloadPrescription.IsDeloadTriggered)
                {
                    targetSets = DeloadTriggerRule.CalculateDeloadSets(targetSets, deloadPrescription.SetReductionFraction);
                    targetRir = DeloadTriggerRule.CalculateDeloadRir(targetRir, deloadPrescription.TargetRirIncrease);
                    progressionAction = ProgressionAction.Regress;
                    rationale = $"Deload active: working sets reduced by {deloadPrescription.SetReductionFraction:P0}, target RIR increased by +{deloadPrescription.TargetRirIncrease}.";
                }
                else if (sanitizedInput.PriorExercisePerformances.TryGetValue(ex.ExerciseId, out var priorPerf))
                {
                    // Double progression evaluation: hit target top reps for 2 consecutive weeks with good technique
                    if (priorPerf.ConsecutiveSuccessWeeks >= StrengthProgressionRule.TwoConsecutiveWeeksForDoubleProgression &&
                        !priorPerf.TechnicalFailureObserved &&
                        priorPerf.PreviousReps >= ex.MaxReps &&
                        priorPerf.PreviousRir >= ex.TargetRir)
                    {
                        var inc = ex.IsCompound ? CompoundProgressionIncrementKg : AccessoryProgressionIncrementKg;
                        targetLoad = priorPerf.PreviousLoadKg + inc;
                        progressionAction = ProgressionAction.Advance;
                        rationale = "Two-consecutive-week target reps reached with clean form and RIR; load increased.";
                    }
                    else
                    {
                        targetLoad = priorPerf.PreviousLoadKg;
                        progressionAction = ProgressionAction.Maintain;
                        rationale = "Target reps or RIR not yet held for two consecutive weeks; maintaining load.";
                    }
                }

                // Injury safety check on individual exercise
                if (sanitizedInput.ActiveInjuries.Count > 0)
                {
                    var maxInjury = sanitizedInput.ActiveInjuries.OrderByDescending(i => i.PainLevel).First();
                    var painDecision = InjurySafetyProgressionRule.EvaluatePainProgression(
                        painLevel: maxInjury.PainLevel,
                        hasNextDayWorsening: maxInjury.HasNextDayWorsening);

                    if (!painDecision.AllowsLoadProgression)
                    {
                        progressionAction = ProgressionAction.Maintain;
                        rationale = $"Injury safety constraint active: {painDecision.ReasonCode}";
                    }
                    if (painDecision.RequiresExerciseStopOrModification || maxInjury.Status == InjurySafetyStatus.Acute)
                    {
                        hasInjuryBlock = true;
                    }
                }

                updatedExercises.Add(ex with
                {
                    TargetSets = targetSets,
                    TargetRir = targetRir,
                    PrescribedLoadKg = targetLoad,
                    ProgressionAction = progressionAction,
                    ProgressionRationale = rationale
                });
            }

            // If deload is triggered, drop optional hard conditioning
            var isHard = session.IsHardSession;
            if (deloadPrescription.IsDeloadTriggered && deloadPrescription.RemoveOptionalHardConditioning && isHard)
            {
                isHard = false;
            }

            finalSessions.Add(session with
            {
                Exercises = updatedExercises,
                IsHardSession = isHard
            });
        }

        trace.Add(new RuleTraceEntry(
            StepNumber: Step5ProgressionAndSafetyGates,
            StepName: "ProgressionAndSafetyGates",
            DecisionSummary: $"Applied progression, injury gates and deload rules. Deload active: {deloadPrescription.IsDeloadTriggered}. Injury blocked: {hasInjuryBlock}.",
            Details: new Dictionary<string, string>
            {
                ["IsDeloadTriggered"] = deloadPrescription.IsDeloadTriggered.ToString(),
                ["DeloadTriggerReason"] = deloadPrescription.TriggerReason,
                ["HasInjuryBlock"] = hasInjuryBlock.ToString()
            }));

        // -------------------------------------------------------------
        // Step 6: Phase & Daily Check-In Precedence
        // -------------------------------------------------------------
        var baselineCycleTier = PhaseAdjustmentRule.SelectTier(sanitizedInput.CyclePhaseBaseline, sanitizedInput.CycleScale);
        var precedenceContext = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: hasInjuryBlock,
            HasAcuteInjuryConflict: hasInjuryBlock,
            BaselineCycleTier: baselineCycleTier,
            CheckInScore: sanitizedInput.TodayCheckIn,
            ApplyChanges: sanitizedInput.ApplyChanges);

        var precedenceResult = PhaseAndCheckInPrecedenceRule.Evaluate(precedenceContext);

        trace.Add(new RuleTraceEntry(
            StepNumber: Step6PhaseCheckInPrecedence,
            StepName: "PhaseAndCheckInPrecedence",
            DecisionSummary: $"Evaluated precedence: Controlling Level = {precedenceResult.ControllingPrecedence}, Effective Tier = {precedenceResult.EffectiveTier}, Applied = {precedenceResult.IsApplied}.",
            Details: new Dictionary<string, string>
            {
                ["ControllingPrecedence"] = precedenceResult.ControllingPrecedence.ToString(),
                ["EffectiveTier"] = precedenceResult.EffectiveTier.ToString(),
                ["ProposalStatus"] = precedenceResult.Status.ToString(),
                ["IsApplied"] = precedenceResult.IsApplied.ToString()
            }));

        // -------------------------------------------------------------
        // Step 7: Final Assembly & Trace
        // -------------------------------------------------------------
        trace.Add(new RuleTraceEntry(
            StepNumber: Step7PlanFinalizationAndTrace,
            StepName: "PlanFinalization",
            DecisionSummary: $"Completed deterministic weekly plan with {finalSessions.Count} scheduled sessions.",
            Details: new Dictionary<string, string>
            {
                ["PlanId"] = planId.ToString(),
                ["TotalSessions"] = finalSessions.Count.ToString(CultureInfo.InvariantCulture),
                ["IsDeloadWeek"] = deloadPrescription.IsDeloadTriggered.ToString()
            }));

        return new DeterministicWeeklyPlanResult(
            PlanId: planId,
            PlanWeekStartDate: sanitizedInput.PlanWeekStartDate,
            GeneratedAtUtc: generatedAt,
            Sessions: finalSessions,
            HypertrophyVolumeSummary: hypertrophyVolumeResult,
            AerobicHealthSummary: aerobicHealthSummary,
            DeloadSummary: deloadPrescription,
            PrecedenceSummary: precedenceResult,
            RuleTrace: trace,
            IsDeloadWeek: deloadPrescription.IsDeloadTriggered,
            MeetsWhoHealthTargets: aerobicHealthSummary.MeetsAllTargets,
            HasSafetyBlock: hasInjuryBlock);
    }

    private static SanitizedWeeklyPlanInput SanitizeInput(WeeklyPlanGeneratorInput? input)
    {
        if (input is null)
        {
            return new SanitizedWeeklyPlanInput(
                Goal: UserGoal.Strength,
                GoalPace: null,
                ProfileExperience: TrainingExperienceLevel.Beginner,
                PreferredActivities: [WorkoutActivityType.GeneralStrength],
                GeneralDaysPerWeek: DefaultGeneralDaysPerWeek,
                PreferredSessionLengthMinutes: DefaultPreferredSessionLengthMinutes,
                AvailableEquipment: Equipment.FullGym,
                StrengthStyle: SessionTrainingStyle.General,
                AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
                WarmupMinutes: DefaultWarmupMinutes,
                CooldownMinutes: DefaultCooldownMinutes,
                ActivityHistories: new Dictionary<WorkoutActivityType, ActivityTrainingHistory>(),
                ExcludedExerciseIds: new HashSet<int>(),
                ActiveInjuries: [],
                PriorExercisePerformances: new Dictionary<int, PriorExercisePerformanceRecord>(),
                RecoveryHistory: [],
                CyclePhaseBaseline: null,
                CycleScale: PhaseBaselineScale.QuizMappedOneToFive,
                TodayCheckIn: null,
                ApplyChanges: false,
                PlanWeekStartDate: DateOnly.FromDateTime(DateTime.UtcNow));
        }

        var clampedDays = Math.Clamp(input.GeneralDaysPerWeek, MinWeeklyDays, MaxWeeklyDays);
        var clampedDuration = Math.Clamp(
            input.PreferredSessionLengthMinutes <= 0 ? DefaultPreferredSessionLengthMinutes : input.PreferredSessionLengthMinutes,
            MinSessionMinutes,
            MaxSessionMinutes);

        var weekdays = input.AvailableWeekdays is { Count: >= MinWeeklyDays }
            ? input.AvailableWeekdays.Distinct().OrderBy(d => (int)d).ToList()
            : [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];

        var activities = input.PreferredActivities is { Count: > 0 }
            ? input.PreferredActivities.Distinct().ToList()
            : [WorkoutActivityType.GeneralStrength];

        var warmup = Math.Max(0, input.WarmupMinutes ?? DefaultWarmupMinutes);
        var cooldown = Math.Max(0, input.CooldownMinutes ?? DefaultCooldownMinutes);

        return new SanitizedWeeklyPlanInput(
            Goal: Enum.IsDefined(input.Goal) ? input.Goal : UserGoal.Strength,
            GoalPace: input.GoalPace,
            ProfileExperience: Enum.IsDefined(input.ProfileExperience) ? input.ProfileExperience : TrainingExperienceLevel.Beginner,
            PreferredActivities: activities,
            GeneralDaysPerWeek: clampedDays,
            PreferredSessionLengthMinutes: clampedDuration,
            AvailableEquipment: Enum.IsDefined(input.AvailableEquipment) ? input.AvailableEquipment : Equipment.FullGym,
            StrengthStyle: Enum.IsDefined(input.StrengthStyle) ? input.StrengthStyle : SessionTrainingStyle.General,
            AvailableWeekdays: weekdays,
            WarmupMinutes: warmup,
            CooldownMinutes: cooldown,
            ActivityHistories: input.ActivityHistories ?? new Dictionary<WorkoutActivityType, ActivityTrainingHistory>(),
            ExcludedExerciseIds: input.ExcludedExerciseIds ?? new HashSet<int>(),
            ActiveInjuries: input.ActiveInjuries ?? [],
            PriorExercisePerformances: input.PriorExercisePerformances ?? new Dictionary<int, PriorExercisePerformanceRecord>(),
            RecoveryHistory: input.RecoveryHistory ?? [],
            CyclePhaseBaseline: input.CyclePhaseBaseline,
            CycleScale: input.CycleScale,
            TodayCheckIn: input.TodayCheckIn,
            ApplyChanges: input.ApplyChanges,
            PlanWeekStartDate: input.PlanWeekStartDate == default ? DateOnly.FromDateTime(DateTime.UtcNow) : input.PlanWeekStartDate);
    }

    private static WorkoutActivityType DetermineStrengthActivity(IReadOnlyList<WorkoutActivityType> activities, SessionTrainingStyle style)
    {
        if (activities.Contains(WorkoutActivityType.HighVolumeStrength) || style == SessionTrainingStyle.HighVolume)
        {
            return WorkoutActivityType.HighVolumeStrength;
        }

        if (activities.Contains(WorkoutActivityType.StrengthHighIntensity) || style == SessionTrainingStyle.HighIntensity)
        {
            return WorkoutActivityType.StrengthHighIntensity;
        }

        return WorkoutActivityType.GeneralStrength;
    }

    private static List<DeterministicPlannedSession> ConstructStrengthSessions(
        WorkoutActivityType strengthActivity,
        int strengthTier,
        int daysPerWeek,
        IReadOnlyList<DayOfWeek> availableWeekdays,
        IReadOnlySet<int> excludedExerciseIds,
        int workBudgetMinutes)
    {
        var sessionCount = Math.Min(daysPerWeek, availableWeekdays.Count);
        var availableSet = availableWeekdays.ToHashSet();
        var assignmentResult = StrengthWeekdayAssignmentRule.AssignBySpecification(sessionCount, availableSet);
        var assignedDays = assignmentResult.Status == StrengthWeekdayAssignmentStatus.Assigned
            ? assignmentResult.Assignments.Select(a => a.Weekday).ToList()
            : availableWeekdays.Take(sessionCount).ToList();

        var sessions = new List<DeterministicPlannedSession>(sessionCount);
        for (var i = 0; i < sessionCount; i++)
        {
            var day = i < assignedDays.Count ? assignedDays[i] : availableWeekdays[i % availableWeekdays.Count];
            var exercises = BuildSessionExercises(i, strengthActivity, strengthTier, excludedExerciseIds, workBudgetMinutes);

            sessions.Add(new DeterministicPlannedSession(
                SessionId: Guid.NewGuid(),
                Activity: strengthActivity,
                DayOfWeek: day,
                EstimatedMinutes: workBudgetMinutes + DefaultWarmupMinutes + DefaultCooldownMinutes,
                IsHardSession: strengthActivity == WorkoutActivityType.StrengthHighIntensity,
                Exercises: exercises,
                Notes: $"Strength Session {i + 1} ({strengthActivity})"));
        }

        return sessions;
    }

    private static List<PlannedExercisePrescription> BuildSessionExercises(
        int sessionIndex,
        WorkoutActivityType strengthActivity,
        int strengthTier,
        IReadOnlySet<int> excludedExerciseIds,
        int workBudgetMinutes)
    {
        var candidateExercises = GetDefaultCandidatesForSession(sessionIndex);
        var result = new List<PlannedExercisePrescription>();
        decimal accumulatedMinutes = 0.0m;

        foreach (var c in candidateExercises)
        {
            if (excludedExerciseIds.Contains(c.ExerciseId))
                continue;

            var sets = c.IsCompound ? DefaultCompoundSets : DefaultAccessorySets;
            var reps = c.IsCompound ? DefaultCompoundMaxReps : DefaultAccessoryMaxReps;
            var restSec = c.IsCompound ? DefaultCompoundRestSeconds : DefaultAccessoryRestSeconds;

            var timeEstimateMinutes = sets * ((reps * DefaultSecondsPerRep + restSec) / SecondsPerMinute);

            // Trim optional accessory if work budget exceeded
            if (accumulatedMinutes + timeEstimateMinutes > workBudgetMinutes && c.IsOptional)
            {
                continue;
            }

            accumulatedMinutes += timeEstimateMinutes;
            result.Add(new PlannedExercisePrescription(
                ExerciseId: c.ExerciseId,
                ExerciseName: c.ExerciseName,
                PrimaryMuscle: c.PrimaryMuscle,
                IsCompound: c.IsCompound,
                IsAccessory: !c.IsCompound,
                IsOptional: c.IsOptional,
                TargetSets: sets,
                MinReps: c.IsCompound ? DefaultCompoundMinReps : DefaultAccessoryMinReps,
                MaxReps: c.IsCompound ? DefaultCompoundMaxReps : DefaultAccessoryMaxReps,
                TargetRir: DefaultTargetRir,
                PrescribedLoadKg: c.DefaultLoadKg,
                ProgressionAction: ProgressionAction.Maintain,
                ProgressionRationale: "Baseline prescription."));
        }

        return result;
    }

    private static (List<DeterministicPlannedSession> Sessions, int HardDaysCount) PackMixedActivities(
        List<DeterministicPlannedSession> strengthSessions,
        IReadOnlyList<WorkoutActivityType> preferredActivities,
        IReadOnlyList<DayOfWeek> availableWeekdays,
        IReadOnlyDictionary<WorkoutActivityType, int> effectiveTiers,
        int preferredDurationMinutes)
    {
        var scheduledDays = strengthSessions.Select(s => s.DayOfWeek).ToHashSet();
        var remainingDays = availableWeekdays.Where(d => !scheduledDays.Contains(d)).ToList();

        var nonStrengthActivities = preferredActivities.Where(a =>
            a != WorkoutActivityType.GeneralStrength &&
            a != WorkoutActivityType.HighVolumeStrength &&
            a != WorkoutActivityType.StrengthHighIntensity &&
            a != WorkoutActivityType.RestDay).ToList();

        var sessions = new List<DeterministicPlannedSession>(strengthSessions);
        var hardDaysCount = strengthSessions.Count(s => s.IsHardSession);

        var dayIdx = 0;
        foreach (var act in nonStrengthActivities)
        {
            if (dayIdx >= remainingDays.Count)
                break;

            var day = remainingDays[dayIdx++];
            var isHard = act == WorkoutActivityType.Hiit;

            if (isHard && hardDaysCount >= MaxHardDaysPerWeek)
            {
                // Hard-day cap reached: omit or downgrade HIIT to moderate cardio
                continue;
            }

            if (isHard)
                hardDaysCount++;

            sessions.Add(new DeterministicPlannedSession(
                SessionId: Guid.NewGuid(),
                Activity: act,
                DayOfWeek: day,
                EstimatedMinutes: preferredDurationMinutes,
                IsHardSession: isHard,
                Exercises: [],
                Notes: $"Cardio/Conditioning Session ({act})"));
        }

        return (sessions.OrderBy(s => (int)s.DayOfWeek).ToList(), hardDaysCount);
    }

    private static Dictionary<MajorMuscleGroup, int> CalculateWeeklySetsByMuscle(List<DeterministicPlannedSession> sessions)
    {
        var dict = new Dictionary<MajorMuscleGroup, int>();
        foreach (var muscle in Enum.GetValues<MajorMuscleGroup>())
        {
            dict[muscle] = 0;
        }

        foreach (var session in sessions)
        {
            foreach (var ex in session.Exercises)
            {
                if (TryParseMajorMuscle(ex.PrimaryMuscle, out var group))
                {
                    dict[group] += ex.TargetSets;
                }
            }
        }

        return dict;
    }

    private static bool TryParseMajorMuscle(string muscleName, out MajorMuscleGroup group)
    {
        if (Enum.TryParse<MajorMuscleGroup>(muscleName, ignoreCase: true, out group))
            return true;

        if (string.Equals(muscleName, "Quadriceps", StringComparison.OrdinalIgnoreCase))
        {
            group = MajorMuscleGroup.Quads;
            return true;
        }
        if (string.Equals(muscleName, "Biceps", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(muscleName, "Triceps", StringComparison.OrdinalIgnoreCase))
        {
            group = MajorMuscleGroup.Arms;
            return true;
        }
        if (string.Equals(muscleName, "Abdominals", StringComparison.OrdinalIgnoreCase))
        {
            group = MajorMuscleGroup.Core;
            return true;
        }

        group = MajorMuscleGroup.Chest;
        return false;
    }

    private static IReadOnlyList<CandidateExerciseSpec> GetDefaultCandidatesForSession(int sessionIndex)
    {
        if (sessionIndex % 2 == 0)
        {
            return
            [
                new(1, "Barbell Back Squat", "Quads", IsCompound: true, IsOptional: false, DefaultLoadKg: 40.0m),
                new(2, "Barbell Bench Press", "Chest", IsCompound: true, IsOptional: false, DefaultLoadKg: 30.0m),
                new(3, "Barbell Bent-Over Row", "Back", IsCompound: true, IsOptional: false, DefaultLoadKg: 30.0m),
                new(4, "Dumbbell Lateral Raise", "Shoulders", IsCompound: false, IsOptional: true, DefaultLoadKg: 6.0m),
                new(5, "Standing Calf Raise", "Calves", IsCompound: false, IsOptional: true, DefaultLoadKg: 20.0m)
            ];
        }

        return
        [
            new(6, "Barbell Romanian Deadlift", "Hamstrings", IsCompound: true, IsOptional: false, DefaultLoadKg: 40.0m),
            new(7, "Overhead Press", "Shoulders", IsCompound: true, IsOptional: false, DefaultLoadKg: 20.0m),
            new(8, "Lat Pulldown", "Back", IsCompound: true, IsOptional: false, DefaultLoadKg: 35.0m),
            new(9, "Barbell Hip Thrust", "Glutes", IsCompound: true, IsOptional: false, DefaultLoadKg: 50.0m),
            new(10, "Bicep Curl", "Arms", IsCompound: false, IsOptional: true, DefaultLoadKg: 8.0m),
            new(11, "Plank Hold", "Core", IsCompound: false, IsOptional: true, DefaultLoadKg: 0.0m)
        ];
    }
}

public sealed record CandidateExerciseSpec(
    int ExerciseId,
    string ExerciseName,
    string PrimaryMuscle,
    bool IsCompound,
    bool IsOptional,
    decimal DefaultLoadKg);

public sealed record PlannerInjuryRecord(
    string Site,
    int PainLevel,
    InjurySafetyStatus Status = InjurySafetyStatus.Acute,
    bool HasNextDayWorsening = false);

public sealed record WeeklyPlanGeneratorInput(
    UserGoal Goal,
    string? GoalPace,
    TrainingExperienceLevel ProfileExperience,
    IReadOnlyList<WorkoutActivityType> PreferredActivities,
    int GeneralDaysPerWeek,
    int PreferredSessionLengthMinutes,
    Equipment AvailableEquipment,
    SessionTrainingStyle StrengthStyle,
    IReadOnlyList<DayOfWeek> AvailableWeekdays,
    int? WarmupMinutes = null,
    int? CooldownMinutes = null,
    IReadOnlyDictionary<WorkoutActivityType, ActivityTrainingHistory>? ActivityHistories = null,
    IReadOnlySet<int>? ExcludedExerciseIds = null,
    IReadOnlyList<PlannerInjuryRecord>? ActiveInjuries = null,
    IReadOnlyDictionary<int, PriorExercisePerformanceRecord>? PriorExercisePerformances = null,
    IReadOnlyList<WeeklyPerformanceAndRecoveryRecord>? RecoveryHistory = null,
    PhaseBaseline? CyclePhaseBaseline = null,
    PhaseBaselineScale CycleScale = PhaseBaselineScale.QuizMappedOneToFive,
    CheckInScoreResult? TodayCheckIn = null,
    bool ApplyChanges = false,
    DateOnly PlanWeekStartDate = default);

internal sealed record SanitizedWeeklyPlanInput(
    UserGoal Goal,
    string? GoalPace,
    TrainingExperienceLevel ProfileExperience,
    IReadOnlyList<WorkoutActivityType> PreferredActivities,
    int GeneralDaysPerWeek,
    int PreferredSessionLengthMinutes,
    Equipment AvailableEquipment,
    SessionTrainingStyle StrengthStyle,
    IReadOnlyList<DayOfWeek> AvailableWeekdays,
    int WarmupMinutes,
    int CooldownMinutes,
    IReadOnlyDictionary<WorkoutActivityType, ActivityTrainingHistory> ActivityHistories,
    IReadOnlySet<int> ExcludedExerciseIds,
    IReadOnlyList<PlannerInjuryRecord> ActiveInjuries,
    IReadOnlyDictionary<int, PriorExercisePerformanceRecord> PriorExercisePerformances,
    IReadOnlyList<WeeklyPerformanceAndRecoveryRecord> RecoveryHistory,
    PhaseBaseline? CyclePhaseBaseline,
    PhaseBaselineScale CycleScale,
    CheckInScoreResult? TodayCheckIn,
    bool ApplyChanges,
    DateOnly PlanWeekStartDate);

public sealed record PriorExercisePerformanceRecord(
    int ExerciseId,
    decimal PreviousLoadKg,
    int PreviousReps,
    int PreviousRir,
    bool TechnicalFailureObserved = false,
    int ConsecutiveSuccessWeeks = 0);

public sealed record PlannedExercisePrescription(
    int ExerciseId,
    string ExerciseName,
    string PrimaryMuscle,
    bool IsCompound,
    bool IsAccessory,
    bool IsOptional,
    int TargetSets,
    int MinReps,
    int MaxReps,
    int TargetRir,
    decimal PrescribedLoadKg,
    ProgressionAction ProgressionAction,
    string ProgressionRationale);

public sealed record DeterministicPlannedSession(
    Guid SessionId,
    WorkoutActivityType Activity,
    DayOfWeek DayOfWeek,
    int EstimatedMinutes,
    bool IsHardSession,
    IReadOnlyList<PlannedExercisePrescription> Exercises,
    string? Notes = null);

public sealed record RuleTraceEntry(
    int StepNumber,
    string StepName,
    string DecisionSummary,
    IReadOnlyDictionary<string, string> Details);

public sealed record DeterministicWeeklyPlanResult(
    Guid PlanId,
    DateOnly PlanWeekStartDate,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<DeterministicPlannedSession> Sessions,
    WeeklyHypertrophyVolumeResult HypertrophyVolumeSummary,
    AerobicHealthEvaluationResult AerobicHealthSummary,
    DeloadPrescription DeloadSummary,
    PrecedenceEvaluationResult PrecedenceSummary,
    IReadOnlyList<RuleTraceEntry> RuleTrace,
    bool IsDeloadWeek,
    bool MeetsWhoHealthTargets,
    bool HasSafetyBlock);
