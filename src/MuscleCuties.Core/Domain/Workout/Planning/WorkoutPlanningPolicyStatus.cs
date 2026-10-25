namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum PolicyEvaluationStatus
{
    Allowed,
    Blocked,
    MissingRequiredInput,
    UnresolvedPolicy
}

public enum PolicyRequirementCategory
{
    HardConstraint,
    DefaultPrescription,
    AspirationalTarget,
    OptionalProposalRequiringConfirmation
}

public static class PolicyEvaluationResult
{
    public static PolicyEvaluationResult<T> Allowed<T>(
        T value,
        string? reasonCode = null,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.DefaultPrescription) =>
        new(PolicyEvaluationStatus.Allowed, value, reasonCode, description, category);

    public static PolicyEvaluationResult<T> Blocked<T>(
        string reasonCode,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.HardConstraint) =>
        new(PolicyEvaluationStatus.Blocked, default, reasonCode, description, category);

    public static PolicyEvaluationResult<T> MissingRequiredInput<T>(
        string reasonCode,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.HardConstraint) =>
        new(PolicyEvaluationStatus.MissingRequiredInput, default, reasonCode, description, category);

    public static PolicyEvaluationResult<T> UnresolvedPolicy<T>(
        string reasonCode,
        string? description = null,
        PolicyRequirementCategory category = PolicyRequirementCategory.DefaultPrescription) =>
        new(PolicyEvaluationStatus.UnresolvedPolicy, default, reasonCode, description, category);
}

public sealed record PolicyEvaluationResult<T>(
    PolicyEvaluationStatus Status,
    T? Value,
    string? ReasonCode,
    string? Description = null,
    PolicyRequirementCategory Category = PolicyRequirementCategory.DefaultPrescription)
{
    public bool IsAllowed => Status == PolicyEvaluationStatus.Allowed;
    public bool IsBlocked => Status == PolicyEvaluationStatus.Blocked;
    public bool IsMissingInput => Status == PolicyEvaluationStatus.MissingRequiredInput;
    public bool IsUnresolved => Status == PolicyEvaluationStatus.UnresolvedPolicy;
}
