using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Health;

public static class HealthEnergyAdjustment
{
    private const int HighActivityWorkoutDays = 5;
    private const int ModerateActivityWorkoutDays = 3;
    private const int LightActivityWorkoutDays = 1;
    private const int HighActivityBaselineSteps = 9000;
    private const int ModerateActivityBaselineSteps = 8000;
    private const int LightActivityBaselineSteps = 7000;
    private const int SedentaryBaselineSteps = 6000;
    private const int StepDifferenceThreshold = 750;

    private const int YoungAgeThreshold = 25;
    private const int OlderAgeThreshold = 50;
    private const float YoungAgeFactor = 1.05f;
    private const float OlderAgeFactor = 0.9f;
    private const float NeutralAgeFactor = 1f;

    private const float MenstrualPhaseFactor = 0.95f;
    private const float LutealPhaseFactor = 1.05f;
    private const float NeutralPhaseFactor = 1f;

    private const float CaloriesPerStepPerKg = 0.0005f;
    private const float MinimumAdjustmentKcal = -180f;
    private const float MaximumAdjustmentKcal = 260f;
    private const float AdjustmentRoundingKcal = 10f;

    public static float CalculateDailyCalories(
        HealthWeeklySummary? summary,
        float weightKg,
        int age,
        int workoutDaysPerWeek,
        CyclePhase phase)
    {
        if (summary is null || !summary.HasMovementData || weightKg <= 0)
            return 0f;

        var baselineSteps = workoutDaysPerWeek switch
        {
            >= HighActivityWorkoutDays => HighActivityBaselineSteps,
            >= ModerateActivityWorkoutDays => ModerateActivityBaselineSteps,
            >= LightActivityWorkoutDays => LightActivityBaselineSteps,
            _ => SedentaryBaselineSteps
        };

        var stepDifference = summary.AverageSteps - baselineSteps;
        if (Math.Abs(stepDifference) < StepDifferenceThreshold)
            return 0f;

        var ageFactor = age switch
        {
            < YoungAgeThreshold => YoungAgeFactor,
            >= OlderAgeThreshold => OlderAgeFactor,
            _ => NeutralAgeFactor
        };
        var phaseFactor = phase switch
        {
            CyclePhase.Menstrual => MenstrualPhaseFactor,
            CyclePhase.Luteal => LutealPhaseFactor,
            _ => NeutralPhaseFactor
        };

        var adjustment = stepDifference * weightKg * CaloriesPerStepPerKg * ageFactor * phaseFactor;
        return MathF.Round(Math.Clamp(adjustment, MinimumAdjustmentKcal, MaximumAdjustmentKcal) /
                           AdjustmentRoundingKcal) * AdjustmentRoundingKcal;
    }
}
