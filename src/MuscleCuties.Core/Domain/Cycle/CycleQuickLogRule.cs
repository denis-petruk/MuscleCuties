using System.Diagnostics.CodeAnalysis;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Cycle;

/// <summary>What she logged for a day, which the decision is made from.</summary>
public readonly record struct CycleQuickLogInput(
    DateTime Date,
    bool? IsPeriodStart,
    bool? IsPeriodEnd,
    FlowIntensity? FlowIntensity,
    IReadOnlyList<SymptomType> Symptoms,
    CyclePhase? PhaseOverride,
    string? Note);

/// <summary>What the quick log decided, including the phase it inferred.</summary>
public readonly record struct CycleQuickLogDecision(
    CyclePhase Phase,
    bool WritePhaseLog,
    string? Note);

/// <summary>Turns a quick log into a phase decision, refusing one that contradicts a recorded start.</summary>
public static class CycleQuickLogRule
{
    [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly",
        Justification = "Preserves ParamName property matching domain input DTO members.")]
    /// <summary>Refuses a log with no date or a phase that cannot follow the recorded history.</summary>
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

    /// <summary>Whether the phase has to be inferred from the flow, rather than taken from her choice.</summary>
    public static bool NeedsInferredPhase(CycleQuickLogInput input)
    {
        return input.IsPeriodStart != true && input.IsPeriodEnd != true && !input.PhaseOverride.HasValue;
    }

    /// <summary>Resolves the day's phase, preferring what she chose over what the flow implies.</summary>
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
