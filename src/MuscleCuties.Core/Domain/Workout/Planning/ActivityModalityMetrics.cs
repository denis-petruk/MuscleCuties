namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// UX02a: Activity-level metric conversions and formatting across modalities
/// (running, cycling, swimming, duration-only recovery).
/// Enforces single conversion seam without fabricated sensor or GPS data.
/// </summary>
public static class ActivityModalityMetrics
{
    public static int? CalculatePaceSecondsPerKm(int durationSeconds, decimal distanceKm)
    {
        if (durationSeconds <= 0 || distanceKm <= 0m)
            return null;

        return (int)Math.Round(durationSeconds / distanceKm);
    }

    public static int? CalculateSwimPaceSecondsPer100m(int durationSeconds, int distanceMeters)
    {
        if (durationSeconds <= 0 || distanceMeters <= 0)
            return null;

        return (int)Math.Round((decimal)durationSeconds / distanceMeters * 100m);
    }

    public static decimal? CalculateSpeedKmPerHour(int durationSeconds, decimal distanceKm)
    {
        if (durationSeconds <= 0 || distanceKm <= 0m)
            return null;

        var hours = (decimal)durationSeconds / 3600m;
        return Math.Round(distanceKm / hours, 1);
    }

    public static string FormatPacePerKm(int? paceSecondsPerKm)
    {
        if (paceSecondsPerKm is null or <= 0)
            return string.Empty;

        var minutes = paceSecondsPerKm.Value / 60;
        var seconds = paceSecondsPerKm.Value % 60;
        return $"{minutes}:{seconds:D2} /km";
    }

    public static string FormatSwimPacePer100m(int? paceSecondsPer100m)
    {
        if (paceSecondsPer100m is null or <= 0)
            return string.Empty;

        var minutes = paceSecondsPer100m.Value / 60;
        var seconds = paceSecondsPer100m.Value % 60;
        return $"{minutes}:{seconds:D2} /100m";
    }

    public static string FormatDuration(int durationSeconds)
    {
        if (durationSeconds <= 0)
            return "0m";

        var totalMinutes = durationSeconds / 60;
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours > 0)
        {
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        return $"{totalMinutes}m";
    }
}
