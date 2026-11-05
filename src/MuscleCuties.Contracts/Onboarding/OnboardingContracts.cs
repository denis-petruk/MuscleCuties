namespace MuscleCuties.Contracts.Onboarding;

public sealed record QuizSelectionDto(int QuestionId, int AnswerId);

public sealed record ProfileSetupDraft(
    string Name,
    DateTime BirthDate,
    float HeightCm,
    float WeightKg,
    string ProfileImagePath,
    int CycleLength,
    IReadOnlyList<int> SelectedActivityTypes,
    bool UseMetricSystem,
    bool UpdateActivityPreferences = true);

public enum ProfileSetupError
{
    None,
    AccountMissing,
    InvalidProfile
}

public sealed record ProfileSetupResult(ProfileSetupError Error, string? ErrorMessage = null)
{
    public bool Succeeded => Error == ProfileSetupError.None;
}

public sealed record ProfileSetupLoadResult(
    bool HasPendingRegistration,
    string? PendingUsername,
    ProfileSetupDraft? Profile);
