namespace MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;

public static class PersonalInfoConstraints
{
    public const int MinimumEligibleAgeYears =
        MuscleCuties.Core.Domain.Onboarding.BirthDateEligibilityRule.MinimumEligibleAgeYears;
}

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

public sealed record LoadPersonalInfoResult(string Email, PersonalInfoProfileDto? Profile);

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

public enum PersonalInfoSaveError { None, InvalidInput, InvalidBirthDate, Underage, AccountMissing }

public sealed record SavePersonalInfoResult(PersonalInfoSaveError Error)
{
    public bool Succeeded => Error == PersonalInfoSaveError.None;
}

public sealed record ChangeAccountPasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ChangeAccountPasswordResult(bool Succeeded, string Message);

public interface IProfilePersonalInfoUseCase
{
    Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken = default);

    Task<SavePersonalInfoResult> SaveAsync(SavePersonalInfoRequest request,
        CancellationToken cancellationToken = default);

    Task<ChangeAccountPasswordResult> ChangePasswordAsync(ChangeAccountPasswordRequest request,
        CancellationToken cancellationToken = default);
}
