namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Whether a policy was satisfied, blocked, or never evaluated.</summary>
public enum PolicyEvaluationStatus
{
    Allowed,
    Blocked,
    MissingRequiredInput,
    UnresolvedPolicy
}

/// <summary>What kind of requirement a policy check belongs to, so gaps group sensibly.</summary>
public enum PolicyRequirementCategory
{
    HardConstraint,
    DefaultPrescription,
    AspirationalTarget,
    OptionalProposalRequiringConfirmation
}

/// <summary>The non-generic result shape the callers share.</summary>
public static class PolicyEvaluationResult
{
    /// <summary>A policy result that permits the action and carries its value.</summary>
    public static PolicyEvaluationResult<T> Allowed<T>(
        T value,
        string? reasonCode = null,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.DefaultPrescription) =>
        new(PolicyEvaluationStatus.Allowed, value, reasonCode, description, category);

    /// <summary>A policy result that refuses the action with a reason.</summary>
    public static PolicyEvaluationResult<T> Blocked<T>(
        string reasonCode,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.HardConstraint) =>
        new(PolicyEvaluationStatus.Blocked, default, reasonCode, description, category);

    /// <summary>A policy result that refuses because an input is missing.</summary>
    public static PolicyEvaluationResult<T> MissingRequiredInput<T>(
        string reasonCode,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.HardConstraint) =>
        new(PolicyEvaluationStatus.MissingRequiredInput, default, reasonCode, description, category);

    /// <summary>A policy result that refuses because the policy itself is unsettled.</summary>
    public static PolicyEvaluationResult<T> UnresolvedPolicy<T>(
        string reasonCode,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.DefaultPrescription) =>
        new(PolicyEvaluationStatus.UnresolvedPolicy, default, reasonCode, description, category);
}

/// <summary>The non-generic result shape the callers share.</summary>
public sealed record PolicyEvaluationResult<T>(
    PolicyEvaluationStatus Status,
    T? Value,
    string? ReasonCode,
    string? Description = null,
    PolicyRequirementCategory Category = PolicyRequirementCategory.DefaultPrescription)
{
    /// <summary>True when the policy permits the action.</summary>
    public bool IsAllowed => Status == PolicyEvaluationStatus.Allowed;
    /// <summary>True when the policy refuses the action.</summary>
    public bool IsBlocked => Status == PolicyEvaluationStatus.Blocked;
    /// <summary>True when the refusal is a missing required input.</summary>
    public bool IsMissingInput => Status == PolicyEvaluationStatus.MissingRequiredInput;
    /// <summary>True when the refusal is an unsettled policy.</summary>
    public bool IsUnresolved => Status == PolicyEvaluationStatus.UnresolvedPolicy;
}
