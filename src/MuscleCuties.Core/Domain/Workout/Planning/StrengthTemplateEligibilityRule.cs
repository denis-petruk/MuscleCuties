using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public static class StrengthTemplateEligibilityRule
{
    // HVS Reason codes
    public const string HvsNotEstablishedHistory = "HVS_NOT_ESTABLISHED_HISTORY";
    public const string HvsProfileLevelTooLow = "HVS_PROFILE_LEVEL_TOO_LOW";
    public const string HvsReturningAfterBreak = "HVS_RETURNING_AFTER_BREAK";
    public const string HvsInjuryConflict = "HVS_INJURY_CONFLICT";
    public const string HvsHardDayConflict = "HVS_HARD_DAY_CONFLICT";
    public const string HvsEligible = "HVS_ELIGIBLE";

    // SHI Reason codes
    public const string ShiNoManageableLoadHistory = "SHI_NO_MANAGEABLE_LOAD_HISTORY";
    public const string ShiInsufficientExposure = "SHI_INSUFFICIENT_EXPOSURE";
    public const string ShiHardDayConflict = "SHI_HARD_DAY_CONFLICT";
    public const string ShiInjuryConflict = "SHI_INJURY_CONFLICT";
    public const string ShiProfileLevelTooLow = "SHI_PROFILE_LEVEL_TOO_LOW";
    public const string ShiReturningAfterBreak = "SHI_RETURNING_AFTER_BREAK";
    public const string ShiEligible = "SHI_ELIGIBLE";

    // HVS Prescriptions
    public const int HvsMainSetsPerExercise = 3;
    public const int HvsAccessorySetsTotal = 2;
    public const int HvsTotalWorkingSetsPerSession = 5;
    public const int HvsMainMinReps = 6;
    public const int HvsMainMaxReps = 12;
    public const int HvsMainTargetRirMin = 2;
    public const int HvsMainTargetRirMax = 3;
    public const int HvsAccessoryMinReps = 10;
    public const int HvsAccessoryMaxReps = 15;
    public const int HvsAccessoryTargetRirMin = 2;
    public const int HvsAccessoryTargetRirMax = 3;

    // SHI Prescriptions
    public const int ShiMainSetsMin = 2;
    public const int ShiMainSetsMax = 3;
    public const int ShiMainMinReps = 4;
    public const int ShiMainMaxReps = 8;
    public const int ShiTargetRir = 2;
    public const int ShiForbiddenRir = 0;
    public const int ShiMaxSessionsPerWeek = 2;

    public static PolicyEvaluationResult<bool> EvaluateHvsEligibility(
        bool isGsEstablished,
        bool isHvsEstablished,
        TrainingExperienceLevel? profileLevel,
        bool isHvsReturning,
        bool hasConflictingInjury,
        bool hasHardDayConflict = false)
    {
        if (hasHardDayConflict)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                HvsHardDayConflict,
                "Cannot schedule HVS due to exceeding hard day limit (at most 1/day, at most 3/week).",
                PolicyRequirementCategory.HardConstraint);
        }

        if (hasConflictingInjury)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                HvsInjuryConflict,
                "HVS is blocked due to active conflicting injury.",
                PolicyRequirementCategory.HardConstraint);
        }

        if (isHvsReturning)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                HvsReturningAfterBreak,
                "HVS is not eligible while returning after an extended break (>=28 days).",
                PolicyRequirementCategory.HardConstraint);
        }

        if (profileLevel is TrainingExperienceLevel.Beginner or null)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                HvsProfileLevelTooLow,
                "HVS requires global experience level above Beginner.",
                PolicyRequirementCategory.HardConstraint);
        }

        if (!isGsEstablished && !isHvsEstablished)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                HvsNotEstablishedHistory,
                "HVS requires established history in GeneralStrength or HighVolumeStrength (>=12 weeks and >=2 sessions/week).",
                PolicyRequirementCategory.HardConstraint);
        }

        return PolicyEvaluationResult.Allowed(
            true,
            HvsEligible,
            "User satisfies all HVS eligibility criteria.",
            PolicyRequirementCategory.DefaultPrescription);
    }

    public static PolicyEvaluationResult<bool> EvaluateShiEligibility(
        bool isGsEstablished,
        bool isShiEstablished,
        TrainingExperienceLevel? profileLevel,
        bool isShiReturning,
        bool hasManageableLoadForAllMainExercises,
        bool hasConflictingInjury,
        bool hasHardDayConflict = false)
    {
        if (hasHardDayConflict)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                ShiHardDayConflict,
                "Cannot schedule SHI due to exceeding hard day limit (at most 1/day, at most 3/week).",
                PolicyRequirementCategory.HardConstraint);
        }

        if (hasConflictingInjury)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                ShiInjuryConflict,
                "SHI is blocked due to active conflicting injury.",
                PolicyRequirementCategory.HardConstraint);
        }

        if (isShiReturning)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                ShiReturningAfterBreak,
                "SHI is not eligible while returning after an extended break (>=28 days).",
                PolicyRequirementCategory.HardConstraint);
        }

        if (profileLevel is TrainingExperienceLevel.Beginner or null)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                ShiProfileLevelTooLow,
                "SHI requires global experience level above Beginner.",
                PolicyRequirementCategory.HardConstraint);
        }

        if (!isGsEstablished && !isShiEstablished)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                ShiInsufficientExposure,
                "SHI requires established history in GeneralStrength or StrengthHighIntensity.",
                PolicyRequirementCategory.HardConstraint);
        }

        if (!hasManageableLoadForAllMainExercises)
        {
            return PolicyEvaluationResult.Blocked<bool>(
                ShiNoManageableLoadHistory,
                "SHI requires manageable-load evidence with acceptable form for every main exercise.",
                PolicyRequirementCategory.HardConstraint);
        }

        return PolicyEvaluationResult.Allowed(
            true,
            ShiEligible,
            "User satisfies all SHI eligibility criteria.",
            PolicyRequirementCategory.DefaultPrescription);
    }
}
