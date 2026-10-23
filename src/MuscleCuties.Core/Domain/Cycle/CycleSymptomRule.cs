using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Cycle;

public static class CycleSymptomRule
{
    public const int MinimumSeverity = 1;
    public const int MaximumSeverity = 5;
    public const int MaximumNotesLength = 1000;

    public static string ValidateAndNormalizeType(string? type)
    {
        var normalizedInput = type?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedInput) ||
            !Enum.GetNames<SymptomType>().Contains(normalizedInput, StringComparer.OrdinalIgnoreCase) ||
            !Enum.TryParse<SymptomType>(normalizedInput, ignoreCase: true, out var symptomType) ||
            !Enum.IsDefined(symptomType))
            throw new ArgumentOutOfRangeException(nameof(type), "Choose a recognized symptom type.");

        return symptomType.ToString().ToLowerInvariant();
    }

    public static void ValidateSeverity(int severity)
    {
        if (severity is < MinimumSeverity or > MaximumSeverity)
            throw new ArgumentOutOfRangeException(nameof(severity), "Symptom severity must be between 1 and 5.");
    }

    public static void ValidateNotes(string? notes)
    {
        if (notes?.Length > MaximumNotesLength)
            throw new ArgumentOutOfRangeException(nameof(notes), "Symptom notes cannot exceed 1000 characters.");
    }
}
