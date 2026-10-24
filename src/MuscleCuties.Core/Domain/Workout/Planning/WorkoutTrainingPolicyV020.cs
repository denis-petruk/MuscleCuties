namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Product rules supplied for MC-WORKOUT-POLICY 0.2.0. This is an implementation
/// configuration, not evidence of clinical validation or an independently signed review.
/// </summary>
public static class WorkoutTrainingPolicyV020
{
    public const string PolicyId = "MC-WORKOUT-POLICY";
    public const string Version = "0.2.0";
    public const string RulesetVersion = "MC-PLANNING-0.2";
    public const string ProductImplementationStatus = "Finalized for this task";
    public const string HistoricalApprovalStatus = "0.1.0 approval reported by product owner";
    // Training prescriptions are authorized by the product owner for this policy version.
    // The check-in score, score-based suggestions, automated progression and
    // injury-specific substitutions retain separate approval gates.
    public static readonly bool CheckInScoreDisplayEnabled;
    public static readonly bool ReadinessSuggestionsEnabled;
    public static readonly bool TrainingPrescriptionPreviewEnabled = true;
    public static readonly bool AutomatedProgressionEnabled;
    public static readonly bool InjurySpecificSubstitutionsEnabled;

    public const int DaysInWeek = 7;
    public const int MinimumConsistentWeeksForEstablished = 12;
    public const int EstablishedSessionsPerWeek = 2;
    public const int EstablishedLookbackWeeks = 4;
    public const int ReturningInactivityDays = 28;
    public const int MaximumGeneratedHardSessionsPerDay = 1;
    public const int InitialMaximumGeneratedHardDaysPerWeek = 3;

    public const int GeneralStrengthWorkingSets = 2;
    public const int EstablishedStrengthWorkingSetsInitially = 2;
    public const int GeneralMainMinimumRepetitions = 8;
    public const int GeneralMainMaximumRepetitions = 12;
    public const int EstablishedMainMinimumRepetitions = 6;
    public const int EstablishedMainMaximumRepetitions = 12;
    public const int AccessoryMinimumRepetitions = 10;
    public const int AccessoryMaximumRepetitions = 15;
    public const int InitialRepetitionsInReserve = 3;
    public const int GeneralMainRestSeconds = 120;
    public const int EstablishedMainRestSeconds = 150;
    public const int AccessoryRestSeconds = 90;
    public const int HighVolumeMainSets = 3;
    public const int HighVolumeAccessorySets = 2;
    public const int HighVolumeMainMinimumRepetitions = 8;
    public const int HighVolumeMainMaximumRepetitions = 12;
    public const int HighVolumeMaximumWorkingExercisesPerSession = 5;
    public const int HighVolumeInitialDirectSetsPerPrimaryMusclePerWeek = 10;
    public const int StrengthHighIntensityMainSets = 3;
    public const int StrengthHighIntensityAccessorySets = 2;
    public const int StrengthHighIntensityMainMinimumRepetitions = 4;
    public const int StrengthHighIntensityMainMaximumRepetitions = 6;
    public const int StrengthHighIntensityMainRestSeconds = 180;
    public const int StrengthHighIntensityMaximumHeavyMainMovementsPerSession = 2;
    public const int StrengthHighIntensityMaximumWorkingExercisesPerSession = 4;
    public const int StrengthHighIntensityMaximumSessionsPerWeekInitially = 2;
    public const int StrengthHighIntensityRequiredExerciseExposures = 4;

    public const int ContinuousCardioWarmupMinutes = 5;
    public const int ContinuousCardioCooldownMinutes = 5;
    public const int BeginnerCardioMainMinutes = 10;
    public const int EstablishedCardioMainMinutesWithoutBaseline = 20;
    public const int BeginnerCardioSessionsPerWeek = 2;
    public const int EstablishedCardioSessionsPerWeekWithoutBaseline = 2;
    public const int SwimmingWarmupMinutes = 5;
    public const int BeginnerClimbingVisitMinutes = 45;
    public const int EstablishedClimbingVisitMinutesWithoutBaseline = 60;
    public const int HiitMaximumSessionsPerWeek = 1;
    public const int HiitRequiredAerobicSessionsPerWeek = 3;
    public const int HiitRequiredPriorWeeks = 4;
    public const int HiitMinimumManageableSessionMinutes = 20;
    public const int HiitWarmupMinutes = 10;
    public const int HiitWorkIntervals = 4;
    public const int HiitWorkIntervalSeconds = 30;
    public const int HiitRecoveryIntervals = 3;
    public const int HiitRecoveryIntervalSeconds = 90;
    public const int HiitCooldownMinutes = 5;
    public const int HiitTotalSeconds = 1290;

    public const int ReadinessScaleMinimum = 1;
    public const int ReadinessScaleMaximum = 5;
    public const int ReadinessScoreMaximum = 100;
    public const int FeelingReadyThreshold = 75;
    public const int ConsiderLighterSessionThreshold = 50;

    public const int CardioProgressionMinutes = 5;
    public const int CardioRegressionMinutes = 5;
    public const int CardioRegressionMinimumBlockMinutes = 5;
    public const int ReturningTemplateInactivityDays = 28;
    public const int LighterWeekSetReduction = 1;
    public const int StrengthProgressionQualifyingExposures = 2;
    public const int StrengthRegressionBelowRangeExposures = 2;
    public const int CardioProgressionQualifyingSessions = 2;
    public const int CardioRegressionEarlyStops = 2;
    public const int RegressionCardioMinimumMainMinutes = 5;
    public const int LighterWeekCardioReductionMinutes = 5;
    public const int GentleYogaMinimumMinutes = 10;
    public const int GentleYogaMaximumMinutes = 20;
    public const int GentleMobilityMinimumMinutes = 5;
    public const int GentleMobilityMaximumMinutes = 15;
    public const int GentleRecoveryMinimumMinutes = 5;
    public const int GentleRecoveryMaximumMinutes = 20;

    /// <summary>HIIT timing is 10m warm-up + 2m work + 4m30s recovery + 5m cool-down.</summary>
    public static TimeSpan HiitSessionDuration => TimeSpan.FromSeconds(HiitTotalSeconds);
}
