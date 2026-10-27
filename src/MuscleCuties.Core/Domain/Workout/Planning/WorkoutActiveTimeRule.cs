namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Only client-measured, server-bounded interval time can be confirmed.</summary>
public static class WorkoutActiveTimeRule
{
    public const int NoSeconds = 0;

    public static bool IsValidCheckpoint(int measuredSeconds, int previouslyConfirmedSeconds, TimeSpan serverElapsed) =>
        previouslyConfirmedSeconds >= NoSeconds && measuredSeconds >= previouslyConfirmedSeconds &&
        serverElapsed >= TimeSpan.Zero && measuredSeconds <= serverElapsed.TotalSeconds;
}
