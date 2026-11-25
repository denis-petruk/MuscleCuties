namespace MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;

/// <summary>The limits the personal-info fields must satisfy, enforced before saving.</summary>
public static class PersonalInfoConstraints
{
    public const int MinimumEligibleAgeYears =
        MuscleCuties.Core.Domain.Onboarding.BirthDateEligibilityRule.MinimumEligibleAgeYears;

    /// <summary>Cycle length the person has not provided; it is never replaced by a default.</summary>
    public const int UnsetCycleLength = 0;
}

/// <summary>The personal info as the page shows it.</summary>
public sealed record PersonalInfoProfileDto(
    string Name,
    DateTime DateOfBirth,
    float Height,
    float Weight,
    int Goal,
    int WeightGoalPace,
    int TrainingExperienceLevel,
    int CycleLength,
    int PregnancyStatus = 0,
    int ContraceptionStatus = 0);

/// <summary>The stored info with the version a save must send back.</summary>
public sealed record LoadPersonalInfoResult(string Email, PersonalInfoProfileDto? Profile);

/// <summary>The edits she is saving, with the version they were based on.</summary>
public sealed record SavePersonalInfoRequest(
    string Email,
    string Name,
    DateTime BirthDate,
    float Height,
    float Weight,
    int Goal,
    int WeightGoalPace,
    int TrainingExperienceLevel,
    int CycleLength,
    int PregnancyStatus = 0,
    int ContraceptionStatus = 0);

/// <summary>Why a save was refused, so the page can point at the field.</summary>
public enum PersonalInfoSaveError { None, InvalidInput, InvalidBirthDate, Underage, AccountMissing }

/// <summary>Whether the save was applied.</summary>
public sealed record SavePersonalInfoResult(PersonalInfoSaveError Error)
{
    /// <summary>True when the save finished without an error.</summary>
    public bool Succeeded => Error == PersonalInfoSaveError.None;
}

/// <summary>A password change, with the current password as proof.</summary>
public sealed record ChangeAccountPasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Whether the change was applied.</summary>
public sealed record ChangeAccountPasswordResult(bool Succeeded, string Message);

/// <summary>The personal-info page.</summary>
public interface IProfilePersonalInfoUseCase
{
    Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken = default);

    Task<SavePersonalInfoResult> SaveAsync(SavePersonalInfoRequest request,
        CancellationToken cancellationToken = default);

    Task<ChangeAccountPasswordResult> ChangePasswordAsync(ChangeAccountPasswordRequest request,
        CancellationToken cancellationToken = default);
}
