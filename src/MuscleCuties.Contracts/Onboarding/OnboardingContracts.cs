namespace MuscleCuties.Contracts.Onboarding;

/// <summary>One answer the user picked for a quiz question.</summary>
public sealed record QuizSelectionDto(int QuestionId, int AnswerId);

/// <summary>
///     What profile setup collected before the quiz. The flag at the end says whether picking activity
///     types should also overwrite the stored workout preferences, which is true during onboarding and
///     false when the screen is reopened from the profile.
/// </summary>
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

/// <summary>Why profile setup did not save; <see cref="None" /> means it did.</summary>
public enum ProfileSetupError
{
    None,
    AccountMissing,
    InvalidProfile
}

/// <summary>Profile setup save result.</summary>
public sealed record ProfileSetupResult(ProfileSetupError Error, string? ErrorMessage = null)
{
    /// <summary>True when the profile was saved.</summary>
    public bool Succeeded => Error == ProfileSetupError.None;
}

/// <summary>
///     What the setup screen should pre-fill: an in-flight registration's username, and the stored
///     profile when the screen is reopened after onboarding.
/// </summary>
public sealed record ProfileSetupLoadResult(
    bool HasPendingRegistration,
    string? PendingUsername,
    ProfileSetupDraft? Profile);
