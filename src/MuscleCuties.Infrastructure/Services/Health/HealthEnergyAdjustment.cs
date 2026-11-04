using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Health;

public static class HealthEnergyAdjustment
{
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
            >= 5 => 9000,
            >= 3 => 8000,
            >= 1 => 7000,
            _ => 6000
        };

        var stepDifference = summary.AverageSteps - baselineSteps;
        if (Math.Abs(stepDifference) < 750)
            return 0f;

        var ageFactor = age switch
        {
            < 25 => 1.05f,
            >= 50 => 0.9f,
            _ => 1f
        };
        var phaseFactor = phase switch
        {
            CyclePhase.Menstrual => 0.95f,
            CyclePhase.Luteal => 1.05f,
            _ => 1f
        };

        var adjustment = stepDifference * weightKg * 0.0005f * ageFactor * phaseFactor;
        return MathF.Round(Math.Clamp(adjustment, -180f, 260f) / 10f) * 10f;
    }
}
