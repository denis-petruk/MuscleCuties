using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.V2;

public enum TrainingFocus
{
    Balanced = 0,
    GluteSpecialization = 1
}

public enum StrengthStyleV2
{
    GeneralStrength = 0,
    HighVolumeStrength = 1,
    StrengthHighIntensity = 2
}

public enum MuscleGroupV2
{
    GluteusMaximus = 0,
    Quadriceps = 1,
    Hamstrings = 2,
    Back = 3,
    Chest = 4,
    LateralDelts = 5,
    RearDelts = 6,
    Biceps = 7,
    Triceps = 8,
    Calves = 9,
    Core = 10,
    HipAbductors = 11,
    HipAdductors = 12,
    AnteriorDelts = 13
}

public sealed record WeeklyVolumeRange(int MinimumSets, int MaximumSets);

public sealed record StrengthStylePrescriptionSpec(
    StrengthStyleV2 Style,
    int TargetRir,
    int DefaultWorkingSets,
    int CompoundMinReps,
    int CompoundMaxReps,
    int CompoundRestSeconds,
    int IsolationMinReps,
    int IsolationMaxReps,
    int IsolationRestSeconds);

public static class WorkoutPolicyV2
{
    public const string PolicyId = "MC-WORKOUT-POLICY";
    public const string Version = "2.0.0";
    public const string RulesetVersion = "MC-PLANNING-2.0";
    public const string InputContractVersion = "MC-PLANNING-INPUT-2.0";

    public const int DetrainingGapDays = 28;
    public const int RollingLoadWindowDays = 7;
    public const int HighRollingStrengthDayCount = 4;
    public const int MaximumWorkingSetsPerExercise = 5;
    public const int MinimumRoutineWorkingSetsPerExercise = 2;
    public const int TargetDirectGluteDays = 2;
    public const int MeaningfulExposureMinimumDirectSets = 2;
    public const int GeneralWarmupMinimumMinutes = 5;
    public const int GeneralWarmupMaximumMinutes = 8;
    public const int DefaultGeneralWarmupMinutes = 6;
    public const int MaximumIndependentSessionsPerDay = 2;
    public const int MaximumPostStrengthFinisherMinutes = 30;
    public const int PainReassessmentHours = 72;
    public const decimal ConservativeReturnLoadMultiplier = 0.875m; // midpoint of 10-15% reduction
    public const int ConservativeReturnSessionCount = 2;
    public const int GlutePlateauObservationWeeks = 3;

    public static StrengthStylePrescriptionSpec GetStyleSpec(StrengthStyleV2 style) => style switch
    {
        StrengthStyleV2.GeneralStrength => new StrengthStylePrescriptionSpec(
            style,
            TargetRir: 3,
            DefaultWorkingSets: 2,
            CompoundMinReps: 8,
            CompoundMaxReps: 12,
            CompoundRestSeconds: 120,
            IsolationMinReps: 10,
            IsolationMaxReps: 15,
            IsolationRestSeconds: 90),
        StrengthStyleV2.HighVolumeStrength => new StrengthStylePrescriptionSpec(
            style,
            TargetRir: 3,
            DefaultWorkingSets: 3,
            CompoundMinReps: 8,
            CompoundMaxReps: 12,
            CompoundRestSeconds: 120,
            IsolationMinReps: 10,
            IsolationMaxReps: 15,
            IsolationRestSeconds: 90),
        StrengthStyleV2.StrengthHighIntensity => new StrengthStylePrescriptionSpec(
            style,
            TargetRir: 1,
            DefaultWorkingSets: 2,
            CompoundMinReps: 5,
            CompoundMaxReps: 8,
            CompoundRestSeconds: 135, // 120-150s approved range
            IsolationMinReps: 8,
            IsolationMaxReps: 12,
            IsolationRestSeconds: 105), // 90-120s approved range
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported strength style.")
    };

    public static StrengthStyleV2 ToStrengthStyle(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.GeneralStrength => StrengthStyleV2.GeneralStrength,
        WorkoutActivityType.HighVolumeStrength => StrengthStyleV2.HighVolumeStrength,
        WorkoutActivityType.StrengthHighIntensity => StrengthStyleV2.StrengthHighIntensity,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Activity is not a strength style.")
    };

    /// <summary>
    /// Gate 58 approved starter direct working-set ranges per week.
    /// </summary>
    public static WeeklyVolumeRange GetWeeklyVolumeTarget(
        MuscleGroupV2 muscle,
        TrainingExperienceLevel experience,
        TrainingFocus focus)
    {
        var tier = experience switch
        {
            TrainingExperienceLevel.Advanced => TrainingExperienceLevel.Advanced,
            TrainingExperienceLevel.Intermediate => TrainingExperienceLevel.Intermediate,
            _ => TrainingExperienceLevel.Beginner
        };

        return (muscle, tier) switch
        {
            (MuscleGroupV2.GluteusMaximus, TrainingExperienceLevel.Beginner) => new(6, 8),
            (MuscleGroupV2.GluteusMaximus, TrainingExperienceLevel.Intermediate) => new(8, 12),
            (MuscleGroupV2.GluteusMaximus, TrainingExperienceLevel.Advanced) => new(10, 16),

            (MuscleGroupV2.Quadriceps, TrainingExperienceLevel.Beginner) => new(4, 6),
            (MuscleGroupV2.Quadriceps, TrainingExperienceLevel.Intermediate) => new(6, 10),
            (MuscleGroupV2.Quadriceps, TrainingExperienceLevel.Advanced) => new(8, 12),

            (MuscleGroupV2.Hamstrings, TrainingExperienceLevel.Beginner) => new(4, 6),
            (MuscleGroupV2.Hamstrings, TrainingExperienceLevel.Intermediate) => new(6, 10),
            (MuscleGroupV2.Hamstrings, TrainingExperienceLevel.Advanced) => new(8, 12),

            (MuscleGroupV2.Back, TrainingExperienceLevel.Beginner) => new(6, 8),
            (MuscleGroupV2.Back, TrainingExperienceLevel.Intermediate) => new(8, 12),
            (MuscleGroupV2.Back, TrainingExperienceLevel.Advanced) => new(10, 14),

            (MuscleGroupV2.Chest, TrainingExperienceLevel.Beginner) => new(4, 6),
            (MuscleGroupV2.Chest, TrainingExperienceLevel.Intermediate) => new(6, 8),
            (MuscleGroupV2.Chest, TrainingExperienceLevel.Advanced) => new(8, 12),

            (MuscleGroupV2.LateralDelts, TrainingExperienceLevel.Beginner) => new(2, 4),
            (MuscleGroupV2.LateralDelts, TrainingExperienceLevel.Intermediate) => new(4, 6),
            (MuscleGroupV2.LateralDelts, TrainingExperienceLevel.Advanced) => new(4, 8),

            (MuscleGroupV2.RearDelts, TrainingExperienceLevel.Beginner) => new(2, 2),
            (MuscleGroupV2.RearDelts, TrainingExperienceLevel.Intermediate) => new(2, 4),
            (MuscleGroupV2.RearDelts, TrainingExperienceLevel.Advanced) => new(2, 6),

            (MuscleGroupV2.Biceps, TrainingExperienceLevel.Beginner) => new(2, 2),
            (MuscleGroupV2.Biceps, TrainingExperienceLevel.Intermediate) => new(2, 4),
            (MuscleGroupV2.Biceps, TrainingExperienceLevel.Advanced) => new(2, 6),

            (MuscleGroupV2.Triceps, TrainingExperienceLevel.Beginner) => new(2, 2),
            (MuscleGroupV2.Triceps, TrainingExperienceLevel.Intermediate) => new(2, 4),
            (MuscleGroupV2.Triceps, TrainingExperienceLevel.Advanced) => new(2, 6),

            (MuscleGroupV2.Calves, TrainingExperienceLevel.Beginner) => new(2, 2),
            (MuscleGroupV2.Calves, TrainingExperienceLevel.Intermediate) => new(2, 4),
            (MuscleGroupV2.Calves, TrainingExperienceLevel.Advanced) => new(2, 6),

            (MuscleGroupV2.Core, TrainingExperienceLevel.Beginner) => new(2, 4),
            (MuscleGroupV2.Core, TrainingExperienceLevel.Intermediate) => new(4, 6),
            (MuscleGroupV2.Core, TrainingExperienceLevel.Advanced) => new(4, 8),

            (MuscleGroupV2.HipAbductors, _) when focus == TrainingFocus.GluteSpecialization => new(2, 4),
            (MuscleGroupV2.HipAbductors, _) => new(0, 0),

            _ => new(0, 0)
        };
    }

    public static bool IsCardioActivitySupportedInV2(WorkoutActivityType activity) => activity is
        WorkoutActivityType.Running or
        WorkoutActivityType.Walking or
        WorkoutActivityType.Cycling or
        WorkoutActivityType.Swimming;
}
