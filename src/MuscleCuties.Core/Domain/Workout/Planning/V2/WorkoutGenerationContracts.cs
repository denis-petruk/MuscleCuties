using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.V2;

/// <summary>Whether an exercise trains a muscle as its target or as support.</summary>
public enum MuscleContributionKind
{
    Incidental = 0,
    Indirect = 1,
    Direct = 2
}

/// <summary>The variety a slot may draw on, which keeps a plan from repeating one movement.</summary>
public enum ExerciseVarietyTag
{
    None = 0,
    HorizontalChestPress = 1,
    InclineChestPress = 2,
    ChestFly = 3,
    ShoulderPress = 4,
    LateralRaise = 5,
    RearDeltDirect = 6,
    WideVerticalPull = 7,
    NeutralVerticalPull = 8,
    HorizontalRow = 9,
    ChestSupportedOrSingleArmRow = 10,
    DynamicCore = 11,
    StaticCore = 12,
    Neck = 13,
    LowerBackIsolation = 14
}

/// <summary>Whether the reviewer cleared an exercise for generation.</summary>
public enum CatalogReviewStatusV2
{
    ApprovedWithConditions = 0,
    PendingOwnerReview = 1,
    Rejected = 2
}

/// <summary>What a set is for, which decides whether it counts toward volume.</summary>
public enum SetKindV2
{
    WarmUp = 0,
    Working = 1
}

/// <summary>Whether a generation produced a plan, was infeasible, or needs her input.</summary>
public enum WorkoutGenerationStatusV2
{
    Feasible = 0,
    Limited = 1,
    InvalidInput = 2,
    SafetyBlocked = 3
}

/// <summary>How the generated week divides its sessions.</summary>
public enum SplitKindV2
{
    FullBody = 0,
    UpperLower = 1,
    Hybrid = 2
}

/// <summary>Whether a session is at full load or reduced, and it is what she sees before starting.</summary>
public enum SessionLoadStatusV2
{
    PrescribedLoad = 0,
    CalibrationRequired = 1,
    BodyweightZeroAddedLoad = 2,
    ConservativeReturnLoad = 3
}

/// <summary>The cardio intensity band a finisher is prescribed at.</summary>
public enum CardioIntensityBandV2
{
    Light = 0,     // RPE 2-3
    Moderate = 1,  // RPE 4-5
    Higher = 2     // RPE 6-7
}

/// <summary>How strongly one exercise trains one muscle, as the generator accounts for it.</summary>
public sealed record ExerciseMuscleContributionV2(
    MuscleGroupV2 Muscle,
    MuscleContributionKind Kind,
    decimal CreditPerWorkingSet)
{
    /// <summary>A contribution that counts fully for the muscle group.</summary>
    public static ExerciseMuscleContributionV2 Direct(MuscleGroupV2 muscle) =>
        new(muscle, MuscleContributionKind.Direct, 1.0m);

    /// <summary>A contribution that counts partially for the muscle group.</summary>
    public static ExerciseMuscleContributionV2 Indirect(MuscleGroupV2 muscle, decimal credit = 0.5m) =>
        new(muscle, MuscleContributionKind.Indirect, credit);
}

/// <summary>One exercise as the generator sees it: its muscles, equipment and constraints.</summary>
public sealed record ExerciseCatalogItemV2(
    int ExerciseId,
    string Name,
    MovementPattern Pattern,
    IReadOnlyList<ExerciseMuscleContributionV2> MuscleContributions,
    IReadOnlySet<ExerciseEquipmentKind> RequiredEquipment,
    IReadOnlySet<TrainingExperienceLevel> SupportedExperiences,
    InjuryFlag Contraindications,
    bool IsCompound,
    bool IsUnilateral,
    bool IsBodyweightOnly,
    decimal SetupMinutes,
    int SecondsPerRep,
    decimal? MinimumLoadIncrementKg,
    string? SupersetCompatibilityGroup = null,
    ExerciseVarietyTag VarietyTag = ExerciseVarietyTag.None,
    CatalogReviewStatusV2 ReviewStatus = CatalogReviewStatusV2.ApprovedWithConditions,
    string ReviewVersion = "MC-CATALOG-V2.0",
    bool IsGluteDominant = false,
    IReadOnlySet<ExerciseTimingMethod>? SupportedTimingMethods = null)
{
    /// <summary>True when the profile's equipment preference is machines or cables.</summary>
    public bool PrefersMachineOrCable =>
        RequiredEquipment.Contains(ExerciseEquipmentKind.Machine) ||
        RequiredEquipment.Contains(ExerciseEquipmentKind.Cable);
}

/// <summary>One set performed in a past session, the evidence progression reads.</summary>
public sealed record ObservedSetRecordV2(
    Guid SessionId,
    int ExerciseId,
    DateOnly LocalDate,
    DateTimeOffset CompletedAtUtc,
    int SetNumber,
    SetKindV2 SetKind,
    int RepsCompleted,
    decimal LoadKg,
    int? RirActual = null,
    bool TechniqueAcceptable = true,
    int? LeftSideReps = null,
    int? RightSideReps = null,
    bool IsDeloadSession = false);

/// <summary>One completed cardio session, the evidence cardio progression reads.</summary>
public sealed record CardioActualRecordV2(
    WorkoutActivityType Activity,
    DateOnly LocalDate,
    int? DurationMinutes,
    decimal? DistanceKm,
    int? Steps,
    int? Rpe,
    int? HeartRateBpm,
    int? CyclingPowerWatts);

/// <summary>A recorded restriction, which bars the exercises it names from the plan.</summary>
public sealed record PainRestrictionV2(
    int? ExerciseId,
    MovementPattern? BlockedPattern,
    InjuryFlag SiteFlag,
    DateTimeOffset? ReportedAtUtc,
    bool IsResolved,
    int ConservativeReturnSessionsCompleted = 0,
    decimal? ReturnBaselineLoadKg = null,
    bool RequiresReturnCalibration = false);

/// <summary>The day's readiness signals the generator weighs.</summary>
public sealed record DailyReadinessInputV2(
    int? EnergyLevel,     // 1-3 (Low/Normal/High) or 1-5
    int? FatigueLevel,    // 1-3 (None/Moderate/High) or 1-5
    int? SleepQuality,    // 1-3 (Poor/Normal/Good) or 1-5
    decimal? SleepHours,
    int? SorenessDoms,    // Always ignored by dosing rules (Gate 89)
    bool HasSystemicRedFlagSymptoms = false,
    bool HasSignificantMenstrualSymptoms = false);

/// <summary>The cycle phase in force, which the generator may let reduce load.</summary>
public sealed record CycleContextInputV2(
    CyclePhase Phase,
    bool IsEstimatedOnly);

/// <summary>One exercise of the plan already accepted, kept so a new plan can avoid churn.</summary>
public sealed record ExistingAcceptedPlanExerciseV2(
    int ExerciseId,
    decimal? WorkingLoadKg);

/// <summary>The plan in force, which a regeneration must not silently replace.</summary>
public sealed record ExistingAcceptedPlanV2(
    Guid PlanId,
    int RevisionNumber,
    IReadOnlyList<ExistingAcceptedPlanExerciseV2> Exercises);

/// <summary>Everything generation is decided from in one payload.</summary>
public sealed record WorkoutGenerationInputV2(
    DateOnly AccountLocalDate,
    string AccountTimeZoneId,
    DateOnly? BirthDate,
    PregnancyStatus PregnancyStatus,
    TrainingExperienceLevel EffectiveExperience,
    int DistinctCompletedWorkoutDays,
    bool IsReturningAfterDetraining,
    UserGoal Goal,
    TrainingFocus Focus,
    StrengthStyleV2 StrengthStyle,
    IReadOnlySet<WorkoutActivityType> SelectedActivities,
    IReadOnlySet<DayOfWeek> AvailableWeekdays,
    IReadOnlyDictionary<WorkoutActivityType, int> SessionDurationMinutesByActivity,
    Equipment EquipmentProfile,
    IReadOnlySet<ExerciseEquipmentKind> AvailableEquipment,
    IReadOnlyList<ExerciseCatalogItemV2> Catalog,
    IReadOnlySet<int>? PermanentlyExcludedExerciseIds = null,
    IReadOnlyList<PainRestrictionV2>? ActivePainRestrictions = null,
    IReadOnlyList<ObservedSetRecordV2>? PriorObservedSets = null,
    IReadOnlyList<CardioActualRecordV2>? PriorCardioActuals = null,
    ExistingAcceptedPlanV2? ExistingAcceptedPlan = null,
    DailyReadinessInputV2? DailyReadiness = null,
    CycleContextInputV2? CycleContext = null,
    int? RequestedStrengthDaysPerWeek = null,
    IReadOnlyList<int>? PriorWeekExerciseIds = null,
    string PolicyVersion = WorkoutPolicyV2.Version,
    string InputContractVersion = WorkoutPolicyV2.InputContractVersion,
    string CatalogVersion = "MC-CATALOG-V2.0",
    IReadOnlySet<DateOnly>? PreviousWeekStrengthDays = null);

/// <summary>One exercise's sets, reps and effort in the generated session.</summary>
public sealed record PlannedExercisePrescriptionV2(
    int OrderIndex,
    int ExerciseId,
    string ExerciseName,
    MovementPattern Pattern,
    bool IsPriorityOrCompound,
    int WarmUpSets,
    int WorkingSets,
    int MinRepetitions,
    int MaxRepetitions,
    int TargetRir,
    int RestSeconds,
    SessionLoadStatusV2 LoadStatus,
    decimal? PrescribedLoadKg,
    decimal EstimatedMinutes,
    int? SupersetGroupId,
    string SelectionReasonCode);

/// <summary>The cardio work appended to a strength session, when the plan asks for it.</summary>
public sealed record CardioFinisherPrescriptionV2(
    WorkoutActivityType Activity,
    int DurationMinutes,
    CardioIntensityBandV2 IntensityBand,
    int TargetRpeMin,
    int TargetRpeMax);

/// <summary>One generated session with its exercises and its load status.</summary>
public sealed record PlannedSessionV2(
    int SessionIndex,
    DayOfWeek Weekday,
    DateOnly LocalDate,
    WorkoutActivityType Activity,
    SplitKindV2? SplitKind,
    int GeneralWarmupMinutes,
    decimal EstimatedTotalMinutes,
    int PhysicalWorkingSets,
    IReadOnlyList<PlannedExercisePrescriptionV2> Exercises,
    CardioFinisherPrescriptionV2? AttachedFinisher = null,
    CardioIntensityBandV2? CardioIntensity = null,
    decimal? CardioDistanceKmTarget = null);

/// <summary>One muscle's accounted sets in the generated week.</summary>
public sealed record MuscleAccountingEntryV2(
    MuscleGroupV2 Muscle,
    decimal DirectSets,
    decimal IndirectSets,
    int MeaningfulDirectDays,
    WeeklyVolumeRange TargetRange,
    bool MeetsTargetVolume);

/// <summary>The week's volume per muscle, which proves the plan met its budgets.</summary>
public sealed record MuscleAccountingSummaryV2(
    int TotalPhysicalWorkingSets,
    IReadOnlyList<MuscleAccountingEntryV2> Muscles,
    bool CoversKneeDominant,
    bool CoversKneeFlexion,
    bool CoversHipExtension,
    bool CoversDirectCalves,
    bool CoversDirectRearDelts,
    int DynamicCoreOccasions,
    bool CoversDirectBiceps,
    bool CoversDirectTriceps,
    bool CoversDirectHipAbduction);

/// <summary>The generated plan with its status and the reasons behind any reduction.</summary>
public sealed record WorkoutGenerationResultV2(
    WorkoutGenerationStatusV2 Status,
    IReadOnlyList<PlannedSessionV2> OriginalSessions,
    IReadOnlyList<PlannedSessionV2> AdaptedSessions,
    MuscleAccountingSummaryV2? CoverageSummary,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyList<string> AdvisoryCodes,
    IReadOnlyList<string> RuleTrace,
    string PolicyVersion,
    string CatalogVersion,
    string NormalizedPrescriptionHash,
    bool ReadinessAdaptationApplied = false,
    string? CoachExplanation = null);
