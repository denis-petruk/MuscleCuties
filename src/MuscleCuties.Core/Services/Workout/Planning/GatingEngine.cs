using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IGatingEngine
{
    GatingResult Resolve(
        WorkoutType slot,
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        int consecutiveLow);
}

public sealed class GatingEngine : IGatingEngine
{
    private readonly WorkoutPlanningConfig _config;

    private static readonly WorkoutActivityType[] LowImpactCardio =
    [
        WorkoutActivityType.Running,
        WorkoutActivityType.Cycling,
        WorkoutActivityType.Swimming
    ];

    public GatingEngine(WorkoutPlanningConfig config)
    {
        _config = config;
    }

    public GatingResult Resolve(
        WorkoutType slot,
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        int consecutiveLow)
    {
        var gating = _config.Gating;

        if (consecutiveLow >= gating.ConsecutiveLowDaysToRest)
        {
            return new GatingResult(
                WorkoutActivityType.Yoga,
                profile.Style,
                0,
                0,
                ["Two low days in a row -- today is a Living happy life day."]);
        }

        if (slot == WorkoutType.Recovery)
        {
            return new GatingResult(WorkoutActivityType.Yoga, profile.Style, 1, 10, []);
        }

        if (slot == WorkoutType.Cardio)
            return ResolveCardio(profile, inputs, readiness, phase, gating);

        if (slot == WorkoutType.Strength)
            return ResolveStrength(profile, inputs, readiness, phase, gating);

        return new GatingResult(WorkoutActivityType.Yoga, profile.Style, 1, 10, []);
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
        {
            return new GatingResult(
                WorkoutActivityType.Yoga,
                profile.Style,
                1,
                10,
                ["Low readiness: cardio swapped for recovery."]);
        }

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
        {
            return new GatingResult(WorkoutActivityType.Hiit, profile.Style, 1, 10, notes);
        }

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

        return new GatingResult(activity, profile.Style, 1, 10, notes);
    }

    private static GatingResult ResolveStrength(
        AdaptiveProfile profile,
        DailyInputs inputs,
        Readiness readiness,
        CyclePhase phase,
        GatingConfig gating)
    {
        var notes = new List<string>();
        var style = profile.Style;
        var baseline = profile.Baselines?.ForPhase(phase);

        if (style == StrengthTrainingStyle.ExpressHard
            && !(readiness.Tier == ReadinessTier.High
                 && (profile.Goal == UserGoal.Strength
                     || profile.Experience == TrainingExperienceLevel.Advanced)))
        {
            style = StrengthTrainingStyle.ComfortableModerate;
            notes.Add("Conditions not met for high-intensity style: defaulting to comfortable-moderate.");
        }

        if (profile.Experience == TrainingExperienceLevel.Beginner)
        {
            style = StrengthTrainingStyle.ComfortableModerate;
        }

        if (baseline is not null && baseline.Pain >= 4 && style == StrengthTrainingStyle.ExpressHard)
        {
            style = StrengthTrainingStyle.ComfortableModerate;
            notes.Add($"Phase baseline pain is high during {phase}: intensity reduced.");
        }

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
                        profile.Style == StrengthTrainingStyle.ExpressHard ||
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
                style,
                setMultiplier,
                gating.LowReadinessRpeCap,
                notes);
        }

        return new GatingResult(activity, style, 1, 10, notes);
    }
}
