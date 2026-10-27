namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Only client-measured, server-bounded interval time can be confirmed.</summary>
public static class WorkoutActiveTimeRule
{
    public const int NoSeconds = 0;
    public const int MaximumUnconfirmedSeconds = 120;
    public const int RoundingAllowanceSeconds = 1;

    public static bool IsValidCheckpoint(int measuredSeconds, int previouslyConfirmedSeconds, TimeSpan serverElapsed) =>
        previouslyConfirmedSeconds >= NoSeconds && measuredSeconds >= previouslyConfirmedSeconds &&
        serverElapsed >= TimeSpan.Zero && measuredSeconds <= serverElapsed.TotalSeconds;

    public static bool IsFreshCheckpoint(int measuredSeconds, int previouslyConfirmedSeconds, TimeSpan sinceLastConfirmation) =>
        sinceLastConfirmation >= TimeSpan.Zero && sinceLastConfirmation <= TimeSpan.FromSeconds(MaximumUnconfirmedSeconds) &&
        measuredSeconds - previouslyConfirmedSeconds <= sinceLastConfirmation.TotalSeconds + RoundingAllowanceSeconds;
}
