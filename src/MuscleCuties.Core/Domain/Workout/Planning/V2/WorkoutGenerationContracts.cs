using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.V2;

public enum MuscleContributionKind
{
    Incidental = 0,
    Indirect = 1,
    Direct = 2
}

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

public enum CatalogReviewStatusV2
{
    ApprovedWithConditions = 0,
    PendingOwnerReview = 1,
    Rejected = 2
}

public enum SetKindV2
{
    WarmUp = 0,
    Working = 1
}

public enum WorkoutGenerationStatusV2
{
    Feasible = 0,
    Limited = 1,
    InvalidInput = 2,
    SafetyBlocked = 3
}

public enum SplitKindV2
{
    FullBody = 0,
    UpperLower = 1,
    Hybrid = 2
}

public enum SessionLoadStatusV2
{
    PrescribedLoad = 0,
    CalibrationRequired = 1,
    BodyweightZeroAddedLoad = 2,
    ConservativeReturnLoad = 3
}

public enum CardioIntensityBandV2
{
    Light = 0,     // RPE 2-3
    Moderate = 1,  // RPE 4-5
    Higher = 2     // RPE 6-7
}

public sealed record ExerciseMuscleContributionV2(
    MuscleGroupV2 Muscle,
    MuscleContributionKind Kind,
    decimal CreditPerWorkingSet)
{
    public static ExerciseMuscleContributionV2 Direct(MuscleGroupV2 muscle) =>
        new(muscle, MuscleContributionKind.Direct, 1.0m);

    public static ExerciseMuscleContributionV2 Indirect(MuscleGroupV2 muscle, decimal credit = 0.5m) =>
        new(muscle, MuscleContributionKind.Indirect, credit);
}

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
    public bool PrefersMachineOrCable =>
        RequiredEquipment.Contains(ExerciseEquipmentKind.Machine) ||
        RequiredEquipment.Contains(ExerciseEquipmentKind.Cable);
}

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

public sealed record CardioActualRecordV2(
    WorkoutActivityType Activity,
    DateOnly LocalDate,
    int? DurationMinutes,
    decimal? DistanceKm,
    int? Steps,
    int? Rpe,
    int? HeartRateBpm,
    int? CyclingPowerWatts);

public sealed record PainRestrictionV2(
    int? ExerciseId,
    MovementPattern? BlockedPattern,
    InjuryFlag SiteFlag,
    DateTimeOffset? ReportedAtUtc,
    bool IsResolved,
    int ConservativeReturnSessionsCompleted = 0,
    decimal? ReturnBaselineLoadKg = null,
    bool RequiresReturnCalibration = false);

public sealed record DailyReadinessInputV2(
    int? EnergyLevel,     // 1-3 (Low/Normal/High) or 1-5
    int? FatigueLevel,    // 1-3 (None/Moderate/High) or 1-5
    int? SleepQuality,    // 1-3 (Poor/Normal/Good) or 1-5
    decimal? SleepHours,
    int? SorenessDoms,    // Always ignored by dosing rules (Gate 89)
    bool HasSystemicRedFlagSymptoms = false,
    bool HasSignificantMenstrualSymptoms = false);

public sealed record CycleContextInputV2(
    CyclePhase Phase,
    bool IsEstimatedOnly);

public sealed record ExistingAcceptedPlanExerciseV2(
    int ExerciseId,
    decimal? WorkingLoadKg);

public sealed record ExistingAcceptedPlanV2(
    Guid PlanId,
    int RevisionNumber,
    IReadOnlyList<ExistingAcceptedPlanExerciseV2> Exercises);

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

public sealed record CardioFinisherPrescriptionV2(
    WorkoutActivityType Activity,
    int DurationMinutes,
    CardioIntensityBandV2 IntensityBand,
    int TargetRpeMin,
    int TargetRpeMax);

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

public sealed record MuscleAccountingEntryV2(
    MuscleGroupV2 Muscle,
    decimal DirectSets,
    decimal IndirectSets,
    int MeaningfulDirectDays,
    WeeklyVolumeRange TargetRange,
    bool MeetsTargetVolume);

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
