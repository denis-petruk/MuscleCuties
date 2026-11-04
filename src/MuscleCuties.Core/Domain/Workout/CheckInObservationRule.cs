namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Validates the supported bounds for one locally recorded check-in.</summary>
public static class CheckInObservationRule
{
    private static readonly DateOnly MinimumSupportedDate = new(1970, 1, 1);
    private static readonly DateOnly MaximumSupportedDate = new(2100, 12, 31);
    private const decimal MinimumSleepHours = 0m;
    private const decimal MaximumSleepHours = 24m;
    private const int MinimumSteps = 0;
    private const int MaximumSteps = 200_000;
    private const int MinimumEnergy = 1;
    private const int MaximumEnergy = 5;
    private const int MinimumPain = 0;
    private const int MaximumPain = 10;
    private const decimal MinimumWeightKg = 10m;
    private const decimal MaximumWeightKg = 500m;
    private const int MinimumSleepQuality = 0;
    private const int MaximumSleepQuality = 4;
    private const int MinimumSoreness = 0;
    private const int MaximumSoreness = 3;
    private const decimal MinimumPreviousDayLoad = 0m;
    private const decimal MaximumPreviousDayLoad = 100_000m;
    private const int MinimumPlanningScaleValue = 1;
    private const int MaximumPlanningScaleValue = 5;

    public static bool IsSupportedRange(Guid userId, DateOnly from, DateOnly through) =>
        userId != Guid.Empty &&
        from >= MinimumSupportedDate &&
        through <= MaximumSupportedDate &&
        through >= from;

    public static bool IsValid(
        Guid userId,
        Guid id,
        DateOnly date,
        decimal? sleepHours,
        decimal? sleep3dAvg,
        int? stepsYesterday,
        int? steps7dAvg,
        int? energy,
        int? pain,
        decimal? weightKg,
        int? sleepQuality,
        int? soreness,
        decimal? previousDayLoad) =>
        userId != Guid.Empty &&
        id != Guid.Empty &&
        date >= MinimumSupportedDate &&
        date <= MaximumSupportedDate &&
        IsWithin(sleepHours, MinimumSleepHours, MaximumSleepHours) &&
        IsWithin(sleep3dAvg, MinimumSleepHours, MaximumSleepHours) &&
        IsWithin(stepsYesterday, MinimumSteps, MaximumSteps) &&
        IsWithin(steps7dAvg, MinimumSteps, MaximumSteps) &&
        IsWithin(energy, MinimumEnergy, MaximumEnergy) &&
        IsWithin(pain, MinimumPain, MaximumPain) &&
        IsWithin(weightKg, MinimumWeightKg, MaximumWeightKg) &&
        IsWithin(sleepQuality, MinimumSleepQuality, MaximumSleepQuality) &&
        IsWithin(soreness, MinimumSoreness, MaximumSoreness) &&
        IsWithin(previousDayLoad, MinimumPreviousDayLoad, MaximumPreviousDayLoad);

    public static bool ArePlanningInputsValid(int? energy, int? sleepQuality, int? soreness, int? stress,
        string? timeZoneId) =>
        ((energy is null && sleepQuality is null && soreness is null && stress is null && timeZoneId is null) ||
         (IsWithin(energy, MinimumEnergy, MaximumEnergy) && energy.HasValue &&
          IsWithin(sleepQuality, MinimumPlanningScaleValue, MaximumPlanningScaleValue) && sleepQuality.HasValue &&
          IsWithin(soreness, MinimumPlanningScaleValue, MaximumPlanningScaleValue) && soreness.HasValue &&
          IsWithin(stress, MinimumPlanningScaleValue, MaximumPlanningScaleValue) && stress.HasValue &&
          timeZoneId is not null && IsValidTimeZoneId(timeZoneId)));

    private static bool IsValidTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return false;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool IsWithin(decimal? value, decimal minimum, decimal maximum) =>
        value is null || value >= minimum && value <= maximum;

    private static bool IsWithin(int? value, int minimum, int maximum) =>
        value is null || value >= minimum && value <= maximum;
}
