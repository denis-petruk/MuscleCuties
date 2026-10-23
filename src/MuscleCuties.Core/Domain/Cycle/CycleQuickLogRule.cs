using System.Diagnostics.CodeAnalysis;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Cycle;

public readonly record struct CycleQuickLogInput(
    DateTime Date,
    bool? IsPeriodStart,
    bool? IsPeriodEnd,
    FlowIntensity? FlowIntensity,
    IReadOnlyList<SymptomType> Symptoms,
    CyclePhase? PhaseOverride,
    string? Note);

public readonly record struct CycleQuickLogDecision(
    CyclePhase Phase,
    bool WritePhaseLog,
    string? Note);

public static class CycleQuickLogRule
{
    [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly",
        Justification = "Preserves ParamName property matching domain input DTO members.")]
    public static void Validate(CycleQuickLogInput input)
    {
        if (input.Date.Date < new DateTime(1970, 1, 1) ||
            input.Date.Date > new DateTime(2100, 12, 31))
            throw new ArgumentOutOfRangeException("Date", "Cycle logs must use a supported calendar date.");

        ArgumentNullException.ThrowIfNull(input.Symptoms);
        foreach (var symptom in input.Symptoms)
            if (!Enum.IsDefined(symptom))
                throw new ArgumentException($"Symptom '{(int)symptom}' is outside the supported vocabulary.",
                    "symptoms");

        if (input.FlowIntensity.HasValue && !Enum.IsDefined(input.FlowIntensity.Value))
            throw new ArgumentException("Choose a supported flow intensity.", "flowIntensity");
        if (input.PhaseOverride.HasValue &&
            (input.PhaseOverride.Value == CyclePhase.Unknown || !Enum.IsDefined(input.PhaseOverride.Value)))
            throw new ArgumentException("Choose a known phase.", "phaseOverride");
        if (input.IsPeriodStart == true && input.IsPeriodEnd == true)
            throw new ArgumentException("Period start and end cannot be recorded for the same day.");
    }

    public static bool NeedsInferredPhase(CycleQuickLogInput input)
    {
        return input.IsPeriodStart != true && input.IsPeriodEnd != true && !input.PhaseOverride.HasValue;
    }

    public static CycleQuickLogDecision Resolve(CycleQuickLogInput input, CyclePhase inferredPhase)
    {
        Validate(input);

        var phase = input.IsPeriodStart == true ? CyclePhase.Menstrual :
            input.IsPeriodEnd == true ? CyclePhase.Follicular :
            input.PhaseOverride ?? inferredPhase;

        if (NeedsInferredPhase(input) &&
            input.FlowIntensity is FlowIntensity.Light or FlowIntensity.Medium or FlowIntensity.Heavy)
            phase = CyclePhase.Menstrual;

        if (phase == CyclePhase.Unknown || !Enum.IsDefined(phase))
            throw new InvalidOperationException(
                "Record a period start before saving symptoms without a known cycle phase.");

        var note = input.IsPeriodStart == true ? input.Note ?? "Period started" :
            input.IsPeriodEnd == true ? input.Note ?? "Period ended" : input.Note;
        return new CycleQuickLogDecision(
            phase,
            !NeedsInferredPhase(input) || input.FlowIntensity.HasValue,
            note);
    }
}
