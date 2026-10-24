using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum PainProgressionDisposition
{
    AllowProgression,
    MaintainOrRegress,
    BlockAndModify
}

public sealed record PainProgressionDecision(
    int PainLevel,
    PainProgressionDisposition Disposition,
    bool AllowsLoadProgression,
    bool RequiresExerciseStopOrModification,
    string ReasonCode);

public sealed record InjuryExerciseSafetyDecision(
    bool IsAllowed,
    bool IsProgressionAllowed,
    decimal PrescribedLoad,
    string SafetyStatus,
    string ReasonCode);

public static class InjurySafetyProgressionRule
{
    public const int MinimumPainLevel = 0;
    public const int MildPainMaximum = 3;
    public const int ModeratePainMaximum = 6;
    public const int HighRiskPainMaximum = 10;

    public const decimal DefaultReturnToTrainingLoadCapFraction = 0.85m;
    public const decimal MinimumSupportedLoad = 0m;

    public const string StatusSafe = "Safe";
    public const string StatusBlockedAcute = "BlockedAcute";
    public const string StatusProgressionBlockedRecovering = "ProgressionBlockedRecovering";
    public const string StatusLoadCappedCleared = "LoadCappedCleared";
    public const string StatusAutoRehabBlocked = "AutoRehabBlocked";

    public const string ReasonAcuteBlocksHeavyOrHighImpact = "AcuteInjuryBlocksHeavyAndHighImpactExercises";
    public const string ReasonRecoveringBlocksProgression = "RecoveringInjuryBlocksProgressionAndAutoRehab";
    public const string ReasonClearedCapsBelowPreInjuryMax = "ClearedInjuryCapsLoadBelowPreInjuryMax";
    public const string ReasonNormalTrainingAllowed = "NormalTrainingAllowed";

    public const string ReasonMildPainTolerableNoNextDayWorsening = "MildPainTolerableNoNextDayWorsening";
    public const string ReasonNextDaySymptomWorseningBlocksProgression = "NextDaySymptomWorseningBlocksProgression";
    public const string ReasonModeratePainSettlingWithinHoursBlocksProgression = "ModeratePainSettlingWithinHoursBlocksProgression";
    public const string ReasonHighRiskPainRequiresStopOrModification = "HighRiskPainRequiresStopOrModification";

    public static PainProgressionDecision EvaluatePainProgression(
        int painLevel,
        bool hasNextDayWorsening,
        bool settlesWithinHours = true)
    {
        var clampedPain = Math.Clamp(painLevel, MinimumPainLevel, HighRiskPainMaximum);

        if (clampedPain <= MildPainMaximum)
        {
            if (hasNextDayWorsening)
            {
                return new PainProgressionDecision(
                    clampedPain,
                    PainProgressionDisposition.MaintainOrRegress,
                    AllowsLoadProgression: false,
                    RequiresExerciseStopOrModification: false,
                    ReasonNextDaySymptomWorseningBlocksProgression);
            }

            return new PainProgressionDecision(
                clampedPain,
                PainProgressionDisposition.AllowProgression,
                AllowsLoadProgression: true,
                RequiresExerciseStopOrModification: false,
                ReasonMildPainTolerableNoNextDayWorsening);
        }

        if (clampedPain <= ModeratePainMaximum)
        {
            return new PainProgressionDecision(
                clampedPain,
                PainProgressionDisposition.MaintainOrRegress,
                AllowsLoadProgression: false,
                RequiresExerciseStopOrModification: false,
                ReasonModeratePainSettlingWithinHoursBlocksProgression);
        }

        return new PainProgressionDecision(
            clampedPain,
            PainProgressionDisposition.BlockAndModify,
            AllowsLoadProgression: false,
            RequiresExerciseStopOrModification: true,
            ReasonHighRiskPainRequiresStopOrModification);
    }

    public static InjuryExerciseSafetyDecision EvaluateExerciseSafety(
        InjuryStatus status,
        InjuryFlag activeInjurySite,
        bool isHeavyMovement,
        bool isHighImpactMovement,
        InjuryFlag exerciseInvolvedSite,
        decimal requestedLoad,
        decimal? preInjuryMaxLoad = null)
    {
        var safeLoad = Math.Max(MinimumSupportedLoad, requestedLoad);
        var siteAffectsExercise = (activeInjurySite & exerciseInvolvedSite) != InjuryFlag.None;

        if (!siteAffectsExercise)
        {
            return new InjuryExerciseSafetyDecision(
                IsAllowed: true,
                IsProgressionAllowed: true,
                safeLoad,
                StatusSafe,
                ReasonNormalTrainingAllowed);
        }

        switch (status)
        {
            case InjuryStatus.Acute:
                if (isHeavyMovement || isHighImpactMovement)
                {
                    return new InjuryExerciseSafetyDecision(
                        IsAllowed: false,
                        IsProgressionAllowed: false,
                        MinimumSupportedLoad,
                        StatusBlockedAcute,
                        ReasonAcuteBlocksHeavyOrHighImpact);
                }

                return new InjuryExerciseSafetyDecision(
                    IsAllowed: true,
                    IsProgressionAllowed: false,
                    safeLoad,
                    StatusBlockedAcute,
                    ReasonAcuteBlocksHeavyOrHighImpact);

            case InjuryStatus.Recovering:
                return new InjuryExerciseSafetyDecision(
                    IsAllowed: true,
                    IsProgressionAllowed: false, // blocks progression, never invent auto-rehab
                    safeLoad,
                    StatusProgressionBlockedRecovering,
                    ReasonRecoveringBlocksProgression);

            case InjuryStatus.Cleared:
                if (preInjuryMaxLoad is { } maxLoad && maxLoad > MinimumSupportedLoad)
                {
                    var cappedLoad = Math.Min(safeLoad, maxLoad * DefaultReturnToTrainingLoadCapFraction);
                    return new InjuryExerciseSafetyDecision(
                        IsAllowed: true,
                        IsProgressionAllowed: true,
                        cappedLoad,
                        StatusLoadCappedCleared,
                        ReasonClearedCapsBelowPreInjuryMax);
                }

                return new InjuryExerciseSafetyDecision(
                    IsAllowed: true,
                    IsProgressionAllowed: true,
                    safeLoad,
                    StatusSafe,
                    ReasonNormalTrainingAllowed);

            default:
                return new InjuryExerciseSafetyDecision(
                    IsAllowed: true,
                    IsProgressionAllowed: true,
                    safeLoad,
                    StatusSafe,
                    ReasonNormalTrainingAllowed);
        }
    }
}
