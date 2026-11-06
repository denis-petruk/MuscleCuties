using System.Globalization;
using MuscleCuties.Core.Domain.Profile;

namespace MuscleCuties.Core.Tests;

public sealed class MeasurementUnitsTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(70d)]
    [InlineData(170d)]
    [InlineData(82.5d)]
    public void Weight_round_trips_through_display_pounds(double kilograms)
    {
        var pounds = MeasurementUnits.ToDisplayWeight(kilograms, useMetricSystem: false);
        var restored = MeasurementUnits.FromDisplayWeight(pounds, useMetricSystem: false);

        Assert.Equal(kilograms, restored, 6);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(55d)]
    [InlineData(170d)]
    [InlineData(203.4d)]
    public void Height_round_trips_through_display_inches(double centimetres)
    {
        var inches = MeasurementUnits.ToDisplayHeight(centimetres, useMetricSystem: false);
        var restored = MeasurementUnits.FromDisplayHeight(inches, useMetricSystem: false);

        Assert.Equal(centimetres, restored, 6);
    }

    [Fact]
    public void Metric_values_pass_through_unchanged()
    {
        Assert.Equal(72.4d, MeasurementUnits.ToDisplayWeight(72.4d, useMetricSystem: true));
        Assert.Equal(168d, MeasurementUnits.ToDisplayHeight(168d, useMetricSystem: true));
        Assert.Equal(5d, MeasurementUnits.ToDisplayDistance(5d, useMetricSystem: true));
    }

    [Fact]
    public void Distance_and_pace_follow_the_selected_system()
    {
        Assert.Equal(5d, MeasurementUnits.KilometresToMiles(8.04672d), 6);

        var metric = MeasurementUnits.FormatPace(360d, useMetricSystem: true, CultureInfo.InvariantCulture);
        var imperial = MeasurementUnits.FormatPace(360d, useMetricSystem: false, CultureInfo.InvariantCulture);

        Assert.Equal("6:00 /km", metric);
        Assert.Equal("9:39 /mi", imperial);
    }

    [Fact]
    public void Weight_labels_name_the_unit_that_the_value_is_shown_in()
    {
        Assert.Equal("72.0 kg", MeasurementUnits.FormatWeight(72d, useMetricSystem: true, CultureInfo.InvariantCulture));
        Assert.Equal("158.7 lb", MeasurementUnits.FormatWeight(72d, useMetricSystem: false, CultureInfo.InvariantCulture));
        Assert.Equal("12.6 km", MeasurementUnits.FormatDistance(12.56d, true, CultureInfo.InvariantCulture));
        Assert.Equal("7.8 mi", MeasurementUnits.FormatDistance(12.56d, false, CultureInfo.InvariantCulture));
    }
}
