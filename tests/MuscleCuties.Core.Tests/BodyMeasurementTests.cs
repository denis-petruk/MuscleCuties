using MuscleCuties.Core.Domain.Progress;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class BodyMeasurementTests
{
    private const double WaistCentimeters = 72.25;
    private const double CorrectedWaistCentimeters = 73.5;
    private const double ArmInches = 12.34;
    private const double InvalidZero = 0;
    private const double InvalidNegative = -1;
    private const double BelowTechnicalMinimum = 0.99;
    private const double TooManyDecimals = 12.345;
    private const double AboveTechnicalMaximum = 501;
    private const int MaximumRepresentableVersion = int.MaxValue;
    private const int StaleVersion = 0;
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    [Theory]
    [InlineData(BodyMeasurementSite.Waist)]
    [InlineData(BodyMeasurementSite.Hips)]
    [InlineData(BodyMeasurementSite.Chest)]
    public void MidlineSitesRequireNoSide(BodyMeasurementSite site)
    {
        var reading = Create(site, BodyMeasurementSide.None);

        Assert.Equal(BodyMeasurementSide.None, reading.Side);
        Assert.Throws<ArgumentException>(() => Create(site, BodyMeasurementSide.Left));
    }

    [Theory]
    [InlineData(BodyMeasurementSite.UpperArm)]
    [InlineData(BodyMeasurementSite.Thigh)]
    [InlineData(BodyMeasurementSite.Calf)]
    public void LimbSitesRequireAnExplicitSide(BodyMeasurementSite site)
    {
        Assert.Equal(BodyMeasurementSide.Left, Create(site, BodyMeasurementSide.Left).Side);
        Assert.Equal(BodyMeasurementSide.Right, Create(site, BodyMeasurementSide.Right).Side);
        Assert.Throws<ArgumentException>(() => Create(site, BodyMeasurementSide.None));
    }

    [Fact]
    public void InchesKeepEnteredValueAndConvertExactlyToCanonicalCentimeters()
    {
        var reading = BodyMeasurement.Create(Guid.NewGuid(), BodyMeasurementSite.UpperArm,
            BodyMeasurementSide.Left, Yesterday, ArmInches, BodyMeasurementUnit.Inches, Today);

        Assert.Equal((decimal)ArmInches, reading.EnteredValue);
        Assert.Equal((decimal)ArmInches * BodyMeasurement.CentimetersPerInch, reading.ValueCentimeters);
        Assert.Equal((decimal)ArmInches,
            reading.ValueCentimeters / BodyMeasurement.CentimetersPerInch);
        Assert.Equal(BodyMeasurementUnit.Inches, reading.EnteredUnit);
    }

    [Theory]
    [InlineData(InvalidZero)]
    [InlineData(InvalidNegative)]
    [InlineData(BelowTechnicalMinimum)]
    [InlineData(TooManyDecimals)]
    [InlineData(AboveTechnicalMaximum)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidReadingsAreRejected(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyMeasurement.Create(Guid.NewGuid(),
            BodyMeasurementSite.Waist, BodyMeasurementSide.None, Yesterday, value,
            BodyMeasurementUnit.Centimeters, Today));
    }

    [Fact]
    public void TechnicalCentimeterBoundsAreInclusive()
    {
        var minimum = BodyMeasurement.Create(Guid.NewGuid(), BodyMeasurementSite.Waist,
            BodyMeasurementSide.None, Yesterday, (double)BodyMeasurement.MinimumCanonicalCentimeters,
            BodyMeasurementUnit.Centimeters, Today);
        var maximum = BodyMeasurement.Create(Guid.NewGuid(), BodyMeasurementSite.Waist,
            BodyMeasurementSide.None, Yesterday, (double)BodyMeasurement.MaximumCanonicalCentimeters,
            BodyMeasurementUnit.Centimeters, Today);

        Assert.Equal(BodyMeasurement.MinimumCanonicalCentimeters, minimum.ValueCentimeters);
        Assert.Equal(BodyMeasurement.MaximumCanonicalCentimeters, maximum.ValueCentimeters);
    }

    [Fact]
    public void FutureDateAndUnknownSiteAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyMeasurement.Create(Guid.NewGuid(),
            BodyMeasurementSite.Waist, BodyMeasurementSide.None, Tomorrow, WaistCentimeters,
            BodyMeasurementUnit.Centimeters, Today));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create((BodyMeasurementSite)int.MaxValue,
            BodyMeasurementSide.None));
    }

    [Fact]
    public void SameDayCorrectionIncrementsVersionAndRejectsStaleVersion()
    {
        var reading = Create(BodyMeasurementSite.Waist, BodyMeasurementSide.None);

        reading.Correct(BodyMeasurement.InitialVersion, BodyMeasurementSite.Waist,
            BodyMeasurementSide.None, Yesterday, CorrectedWaistCentimeters,
            BodyMeasurementUnit.Centimeters, Today);

        Assert.Equal(Yesterday, reading.LocalDate);
        Assert.Equal((decimal)CorrectedWaistCentimeters, reading.ValueCentimeters);
        Assert.Equal(BodyMeasurement.InitialVersion + 1, reading.Version);
        Assert.Throws<InvalidOperationException>(() => reading.Correct(StaleVersion,
            BodyMeasurementSite.Waist, BodyMeasurementSide.None, Yesterday, WaistCentimeters,
            BodyMeasurementUnit.Centimeters, Today));
        Assert.Equal((decimal)CorrectedWaistCentimeters, reading.ValueCentimeters);
    }

    [Fact]
    public void DailySlotDistinguishesSideDateAndRecordIdentity()
    {
        var left = Create(BodyMeasurementSite.Calf, BodyMeasurementSide.Left);
        var duplicate = Create(BodyMeasurementSite.Calf, BodyMeasurementSide.Left);
        var right = Create(BodyMeasurementSite.Calf, BodyMeasurementSide.Right);
        var later = BodyMeasurement.Create(Guid.NewGuid(), BodyMeasurementSite.Calf,
            BodyMeasurementSide.Left, Today, WaistCentimeters, BodyMeasurementUnit.Centimeters, Today);

        Assert.True(left.OccupiesSameDailySlotAs(duplicate));
        Assert.False(left.OccupiesSameDailySlotAs(right));
        Assert.False(left.OccupiesSameDailySlotAs(later));
        Assert.False(left.OccupiesSameDailySlotAs(left));
    }

    [Fact]
    public void VersionedDeletionReleasesSlotAndPreventsLaterChanges()
    {
        var reading = Create(BodyMeasurementSite.Waist, BodyMeasurementSide.None);
        var other = Create(BodyMeasurementSite.Waist, BodyMeasurementSide.None);

        reading.Delete(BodyMeasurement.InitialVersion);

        Assert.True(reading.IsDeleted);
        Assert.Equal(BodyMeasurement.InitialVersion + 1, reading.Version);
        Assert.False(reading.OccupiesSameDailySlotAs(other));
        Assert.Throws<InvalidOperationException>(() => reading.Delete(reading.Version));
        Assert.Throws<InvalidOperationException>(() => reading.Correct(reading.Version,
            BodyMeasurementSite.Waist, BodyMeasurementSide.None, Yesterday, WaistCentimeters,
            BodyMeasurementUnit.Centimeters, Today));
    }

    [Fact]
    public void RestoredReadingRetainsVersionAndRejectsInvalidPersistedState()
    {
        var id = Guid.NewGuid();
        var restored = BodyMeasurement.Restore(id, BodyMeasurementSite.Calf,
            BodyMeasurementSide.Right, Yesterday, (decimal)ArmInches,
            BodyMeasurementUnit.Inches, BodyMeasurement.InitialVersion + 1, false, Today);

        Assert.Equal(id, restored.Id);
        Assert.Equal((decimal)ArmInches * BodyMeasurement.CentimetersPerInch,
            restored.ValueCentimeters);
        Assert.Equal(BodyMeasurement.InitialVersion + 1, restored.Version);
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyMeasurement.Restore(id,
            BodyMeasurementSite.Calf, BodyMeasurementSide.Right, Yesterday,
            (decimal)ArmInches, BodyMeasurementUnit.Inches, StaleVersion, false, Today));
    }

    [Fact]
    public void InvalidCorrectionDoesNotPartiallyChangeReading()
    {
        var reading = Create(BodyMeasurementSite.Waist, BodyMeasurementSide.None);

        Assert.Throws<ArgumentException>(() => reading.Correct(reading.Version,
            BodyMeasurementSite.UpperArm, BodyMeasurementSide.None, Today,
            CorrectedWaistCentimeters, BodyMeasurementUnit.Centimeters, Today));

        Assert.Equal(BodyMeasurementSite.Waist, reading.Site);
        Assert.Equal(Yesterday, reading.LocalDate);
        Assert.Equal((decimal)WaistCentimeters, reading.ValueCentimeters);
        Assert.Equal(BodyMeasurement.InitialVersion, reading.Version);
    }

    [Fact]
    public void VersionOverflowCannotPartiallyCorrectOrDeleteRestoredReading()
    {
        var reading = BodyMeasurement.Restore(Guid.NewGuid(), BodyMeasurementSite.Waist,
            BodyMeasurementSide.None, Yesterday, (decimal)WaistCentimeters,
            BodyMeasurementUnit.Centimeters, MaximumRepresentableVersion, false, Today);

        Assert.Throws<OverflowException>(() => reading.Correct(MaximumRepresentableVersion,
            BodyMeasurementSite.Waist, BodyMeasurementSide.None, Today,
            CorrectedWaistCentimeters, BodyMeasurementUnit.Centimeters, Today));
        Assert.Equal(Yesterday, reading.LocalDate);
        Assert.Equal((decimal)WaistCentimeters, reading.ValueCentimeters);
        Assert.Equal(MaximumRepresentableVersion, reading.Version);
        Assert.False(reading.IsDeleted);

        Assert.Throws<OverflowException>(() => reading.Delete(MaximumRepresentableVersion));
        Assert.Equal(MaximumRepresentableVersion, reading.Version);
        Assert.False(reading.IsDeleted);
    }

    private static BodyMeasurement Create(BodyMeasurementSite site, BodyMeasurementSide side) =>
        BodyMeasurement.Create(Guid.NewGuid(), site, side, Yesterday, WaistCentimeters,
            BodyMeasurementUnit.Centimeters, Today);
}
