using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout;

public static class WorkoutGatingRule
{
    private static readonly WorkoutActivityType[] LowImpactCardio =
    [
        WorkoutActivityType.Running,
        WorkoutActivityType.Cycling,
        WorkoutActivityType.Swimming
    ];

    public static GatingResult Resolve(
        WorkoutType slot,
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        int consecutiveLow,
        WorkoutPlanningConfig? config)
    {
        if (profile is null || inputs is null || readiness is null ||
            profile.Selected is null || config?.Gating is not { } gating ||
            !Enum.IsDefined(slot))
            return ConservativeDefault();
        inputs = DailyInputs.CreateSafe(inputs.Date, inputs.SleepHours, inputs.Sleep3dAvg,
            inputs.StepsYesterday, inputs.Steps7dAvg, inputs.Energy, inputs.Pain,
            inputs.Bloating, inputs.WeightKg, inputs.RestingHeartRate,
            inputs.AverageActiveEnergyKcal, inputs.SleepQuality, inputs.Soreness,
            inputs.PreviousDayLoad);
        if (!Enum.IsDefined(readiness.Tier) || readiness.Score is < 0 or > 100)
            readiness = readiness with { Score = 0, Tier = ReadinessTier.Low };
        if (phase == CyclePhase.Unknown || !Enum.IsDefined(phase))
            return ConservativeDefault();

        var lowDayThreshold = gating.ConsecutiveLowDaysToRest > 0
            ? gating.ConsecutiveLowDaysToRest
            : 2;

        if (Math.Max(0, consecutiveLow) >= lowDayThreshold)
            return new GatingResult(
                WorkoutActivityType.Yoga,
                0,
                0,
                ["Two low days in a row -- today is a Living happy life day."]);

        if (slot == WorkoutType.Recovery) return new GatingResult(WorkoutActivityType.Yoga, 1, 10, []);

        if (slot == WorkoutType.Cardio)
            return ResolveCardio(profile, inputs, readiness, phase, gating);

        if (slot == WorkoutType.Strength)
            return ResolveStrength(profile, inputs, readiness, phase, gating);

        return new GatingResult(WorkoutActivityType.Yoga, 1, 10, []);
    }

    private static GatingResult ResolveCardio(
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        GatingConfig gating)
    {
        var notes = new List<string>();
        var baseline = profile.Baselines?.ForPhase(phase);

        if (readiness.Tier == ReadinessTier.Low)
            return new GatingResult(
                WorkoutActivityType.Yoga,
                1,
                10,
                ["Low readiness: cardio swapped for recovery."]);

        var menstrualEnergyThreshold = gating.HiitMenstrualMinEnergy;
        if (baseline is not null && phase == CyclePhase.Menstrual && baseline.Energy <= 2)
            menstrualEnergyThreshold = 5;

        var hiitEligible =
            profile.Selected.Contains(WorkoutActivityType.Hiit)
            && readiness.Tier != ReadinessTier.Low
            && inputs.Pain <= gating.HiitMaxPain
            && inputs.SleepHours >= gating.HiitMinSleep
            && (phase != CyclePhase.Menstrual || inputs.Energy >= menstrualEnergyThreshold)
            && phase != CyclePhase.Luteal;

        if (baseline is not null && baseline.Pain >= 4)
        {
            hiitEligible = false;
            notes.Add($"Phase baseline pain is high: HIIT skipped for {phase}.");
        }

        if (hiitEligible
            && (phase == CyclePhase.Ovulatory
                || profile.Goal == UserGoal.FatLoss
                || readiness.Tier == ReadinessTier.High))
            return new GatingResult(WorkoutActivityType.Hiit, 1, 10, notes);

        var lowImpactThreshold = baseline is not null ? baseline.Energy : 3;
        var lowImpact = (phase == CyclePhase.Luteal || phase == CyclePhase.Menstrual)
                        && inputs.Energy <= lowImpactThreshold;

        var preferred = LowImpactCardio
            .Where(a => profile.Selected.Contains(a))
            .ToList();

        if (lowImpact
            && preferred.Contains(WorkoutActivityType.Running)
            && preferred.Count > 1)
        {
            preferred.Remove(WorkoutActivityType.Running);
            notes.Add("Low energy during this phase: running swapped for lower-impact cardio.");
        }

        var activity = preferred.Count > 0 ? preferred[0] : WorkoutActivityType.Yoga;

        return new GatingResult(activity, 1, 10, notes);
    }

    private static GatingResult ResolveStrength(
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        GatingConfig gating)
    {
        var notes = new List<string>();
        var baseline = profile.Baselines?.ForPhase(phase);

        var menstrualEnergyThreshold = gating.HiitMenstrualMinEnergy;
        if (baseline is not null && phase == CyclePhase.Menstrual && baseline.Energy <= 2)
            menstrualEnergyThreshold = 5;

        var intensityOk =
            profile.Selected.Contains(WorkoutActivityType.StrengthHighIntensity)
            && readiness.Tier != ReadinessTier.Low
            && inputs.Pain <= 1
            && profile.Experience != TrainingExperienceLevel.Beginner
            && (phase != CyclePhase.Menstrual || inputs.Energy >= menstrualEnergyThreshold);

        var activity = intensityOk &&
                       (profile.Goal == UserGoal.Strength ||
                        phase == CyclePhase.Ovulatory)
            ? WorkoutActivityType.StrengthHighIntensity
            : WorkoutActivityType.HighVolumeStrength;

        if (readiness.Tier == ReadinessTier.Low)
        {
            var setMultiplier = gating.LowReadinessSetMultiplier;
            if (baseline is not null && baseline.Energy <= 2)
                setMultiplier = Math.Min(setMultiplier, 0.6);

            notes.Add("Low readiness: volume and intensity reduced.");
            return new GatingResult(
                WorkoutActivityType.HighVolumeStrength,
                double.IsFinite(setMultiplier) ? Math.Clamp(setMultiplier, 0, 1) : 0.6,
                gating.LowReadinessRpeCap,
                notes);
        }

        return new GatingResult(activity, 1, 10, notes);
    }

    private static GatingResult ConservativeDefault()
    {
        return new GatingResult(
            WorkoutActivityType.Yoga,
            0,
            0,
            ["Planning inputs are unavailable; choose rest or gentle movement."]);
    }
}
