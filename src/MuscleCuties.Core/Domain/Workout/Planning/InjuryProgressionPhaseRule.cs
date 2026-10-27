namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Calendar classification only; a phase is not evidence of clinical approval.</summary>
public enum InjuryProgressionPhase { Unknown, Active, ClearedOptionalWindow, ClearedAfterWindow }

public static class InjuryProgressionPhaseRule
{
    public static InjuryProgressionPhase Classify(InjuryRestorationEpisode? episode, DateOnly? accountLocalDate)
    {
        if (episode is null || accountLocalDate is null) return InjuryProgressionPhase.Unknown;
        if (episode.IsActive)
            return episode.ClearedOn is null ? InjuryProgressionPhase.Active : InjuryProgressionPhase.Unknown;
        if (episode.ClearedOn is not { } clearance || clearance > accountLocalDate ||
            WorkoutInjuryClearanceRule.RequiredFromLocalDate(clearance) is not { } requiredFrom)
            return InjuryProgressionPhase.Unknown;
        return accountLocalDate.Value < requiredFrom
            ? InjuryProgressionPhase.ClearedOptionalWindow
            : InjuryProgressionPhase.ClearedAfterWindow;
    }
}
