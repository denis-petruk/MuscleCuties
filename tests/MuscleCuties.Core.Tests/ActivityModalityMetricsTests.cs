using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class ActivityModalityMetricsTests
{
    [Fact]
    public void CalculatePaceSecondsPerKm_CalculatesPaceCorrectly()
    {
        // 5 km in 25 minutes (1500 seconds) = 300 sec/km (5:00 /km)
        var pace = ActivityModalityMetrics.CalculatePaceSecondsPerKm(1500, 5.0m);
        Assert.Equal(300, pace);

        var formatted = ActivityModalityMetrics.FormatPacePerKm(pace);
        Assert.Equal("5:00 /km", formatted);
    }

    [Fact]
    public void CalculatePaceSecondsPerKm_HandlesFractionalPaces()
    {
        // 10 km in 52m 30s (3150 seconds) = 315 sec/km (5:15 /km)
        var pace = ActivityModalityMetrics.CalculatePaceSecondsPerKm(3150, 10.0m);
        Assert.Equal(315, pace);

        var formatted = ActivityModalityMetrics.FormatPacePerKm(pace);
        Assert.Equal("5:15 /km", formatted);
    }

    [Fact]
    public void CalculateSwimPaceSecondsPer100m_CalculatesSwimPaceCorrectly()
    {
        // 1000m swim in 20 minutes (1200 seconds) = 120 sec / 100m (2:00 /100m)
        var swimPace = ActivityModalityMetrics.CalculateSwimPaceSecondsPer100m(1200, 1000);
        Assert.Equal(120, swimPace);

        var formatted = ActivityModalityMetrics.FormatSwimPacePer100m(swimPace);
        Assert.Equal("2:00 /100m", formatted);
    }

    [Fact]
    public void CalculateSpeedKmPerHour_CalculatesCyclingSpeed()
    {
        // 30 km in 1 hour (3600 seconds) = 30.0 km/h
        var speed = ActivityModalityMetrics.CalculateSpeedKmPerHour(3600, 30.0m);
        Assert.Equal(30.0m, speed);

        // 45 km in 90 minutes (5400 seconds) = 30.0 km/h
        var speed90m = ActivityModalityMetrics.CalculateSpeedKmPerHour(5400, 45.0m);
        Assert.Equal(30.0m, speed90m);
    }

    [Fact]
    public void FormatDuration_FormatsHoursAndMinutes()
    {
        Assert.Equal("45m", ActivityModalityMetrics.FormatDuration(2700));
        Assert.Equal("1h", ActivityModalityMetrics.FormatDuration(3600));
        Assert.Equal("1h 15m", ActivityModalityMetrics.FormatDuration(4500));
        Assert.Equal("0m", ActivityModalityMetrics.FormatDuration(0));
    }
}
