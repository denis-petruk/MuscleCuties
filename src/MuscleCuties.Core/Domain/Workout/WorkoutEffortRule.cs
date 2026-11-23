using System.Globalization;

namespace MuscleCuties.Core.Domain.Workout;

/// <summary>
/// The one owner of effort entry on a workout set. Effort is a whole rating from 1 to 10; an untouched
/// centred slider is a neutral position rather than a recorded rating, so it must never be committed
/// without the user moving it.
/// </summary>
public static class WorkoutEffortRule
{
    public const int MinimumEffort = 1;
    public const int MaximumEffort = 10;
    public const int NeutralSliderPosition = 5;

    public static bool TryParseLogged(string? text, out int effort)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out effort))
            return false;

        if (effort is < MinimumEffort or > MaximumEffort)
        {
            effort = 0;
            return false;
        }

        return true;
    }

    public static bool TryRoundSlider(double value, out int effort)
    {
        if (!double.IsFinite(value))
        {
            effort = 0;
            return false;
        }

        effort = (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), MinimumEffort, MaximumEffort);
        return true;
    }

    public static bool IsUntouchedNeutralThumb(int effort, bool hasRecordedEffort) =>
        !hasRecordedEffort && effort == NeutralSliderPosition;

    public static string Format(int effort) =>
        $"{effort.ToString(CultureInfo.InvariantCulture)} / {MaximumEffort.ToString(CultureInfo.InvariantCulture)}";
}
