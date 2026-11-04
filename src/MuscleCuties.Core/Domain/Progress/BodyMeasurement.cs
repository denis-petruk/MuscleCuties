namespace MuscleCuties.Core.Domain.Progress;

public enum BodyMeasurementSite
{
    Waist,
    Hips,
    Chest,
    UpperArm,
    Thigh,
    Calf
}

public enum BodyMeasurementSide
{
    None,
    Left,
    Right
}

public enum BodyMeasurementUnit
{
    Centimeters,
    Inches
}

/// <summary>
/// One manually reported tape reading. Technical limits guard input and storage; they are not clinical ranges.
/// Owner identity and database uniqueness belong to the authenticated persistence boundary.
/// </summary>
public sealed class BodyMeasurement
{
    public const decimal MinimumCanonicalCentimeters = 1m;
    public const decimal MaximumCanonicalCentimeters = 500m;
    public const int MaximumInputDecimalPlaces = 2;
    public const decimal InputDecimalScale = 100m;
    public const decimal CentimetersPerInch = 2.54m;
    public const int InitialVersion = 1;

    private BodyMeasurement(Guid id, BodyMeasurementSite site, BodyMeasurementSide side,
        DateOnly localDate, decimal enteredValue, BodyMeasurementUnit enteredUnit, decimal valueCentimeters)
    {
        Id = id;
        Site = site;
        Side = side;
        LocalDate = localDate;
        EnteredValue = enteredValue;
        EnteredUnit = enteredUnit;
        ValueCentimeters = valueCentimeters;
        Version = InitialVersion;
    }

    public Guid Id { get; }
    public BodyMeasurementSite Site { get; private set; }
    public BodyMeasurementSide Side { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public decimal EnteredValue { get; private set; }
    public BodyMeasurementUnit EnteredUnit { get; private set; }
    public decimal ValueCentimeters { get; private set; }
    public int Version { get; private set; }
    public bool IsDeleted { get; private set; }

    public static void RequireCommandId(Guid commandId)
    {
        if (commandId == Guid.Empty)
            throw new ArgumentException("A stable command ID is required for safe retries.", nameof(commandId));
    }

    public static void RequireHistoryRange(DateOnly from, DateOnly through, DateOnly today)
    {
        if (from > through || through > today)
            throw new ArgumentOutOfRangeException(nameof(from), "History range must end no later than today.");
    }

    public static BodyMeasurement Create(Guid id, BodyMeasurementSite site, BodyMeasurementSide side,
        DateOnly localDate, double enteredValue, BodyMeasurementUnit enteredUnit, DateOnly today)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Measurement ID is required.", nameof(id));

        var (value, centimeters) = Validate(site, side, localDate, enteredValue, enteredUnit, today);
        return new BodyMeasurement(id, site, side, localDate, value, enteredUnit, centimeters);
    }

    public static BodyMeasurement Restore(Guid id, BodyMeasurementSite site, BodyMeasurementSide side,
        DateOnly localDate, decimal enteredValue, BodyMeasurementUnit enteredUnit,
        int version, bool isDeleted, DateOnly today)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Measurement ID is required.", nameof(id));
        ArgumentOutOfRangeException.ThrowIfLessThan(version, InitialVersion);

        var (value, centimeters) = Validate(site, side, localDate, (double)enteredValue, enteredUnit, today);
        return new BodyMeasurement(id, site, side, localDate, value, enteredUnit, centimeters)
        {
            Version = version,
            IsDeleted = isDeleted
        };
    }

    public void Correct(int expectedVersion, BodyMeasurementSite site, BodyMeasurementSide side,
        DateOnly localDate, double enteredValue, BodyMeasurementUnit enteredUnit, DateOnly today)
    {
        EnsureCurrentVersion(expectedVersion);
        var nextVersion = checked(Version + 1);
        var (value, centimeters) = Validate(site, side, localDate, enteredValue, enteredUnit, today);
        Site = site;
        Side = side;
        LocalDate = localDate;
        EnteredValue = value;
        EnteredUnit = enteredUnit;
        ValueCentimeters = centimeters;
        Version = nextVersion;
    }

    public void Delete(int expectedVersion)
    {
        EnsureCurrentVersion(expectedVersion);
        var nextVersion = checked(Version + 1);
        IsDeleted = true;
        Version = nextVersion;
    }

    public bool OccupiesSameDailySlotAs(BodyMeasurement other) =>
        other is not null && !IsDeleted && !other.IsDeleted && Id != other.Id &&
        Site == other.Site && Side == other.Side && LocalDate == other.LocalDate;

    private void EnsureCurrentVersion(int expectedVersion)
    {
        if (IsDeleted)
            throw new InvalidOperationException("A deleted measurement cannot be changed.");
        if (expectedVersion != Version)
            throw new InvalidOperationException("The measurement version is stale.");
    }

    private static (decimal Value, decimal Centimeters) Validate(BodyMeasurementSite site,
        BodyMeasurementSide side, DateOnly localDate, double enteredValue,
        BodyMeasurementUnit enteredUnit, DateOnly today)
    {
        if (!Enum.IsDefined(site))
            throw new ArgumentOutOfRangeException(nameof(site));
        if (!Enum.IsDefined(side) || (IsMidline(site) ? side != BodyMeasurementSide.None : side == BodyMeasurementSide.None))
            throw new ArgumentException("The selected side is invalid for this site.", nameof(side));
        if (!Enum.IsDefined(enteredUnit))
            throw new ArgumentOutOfRangeException(nameof(enteredUnit));
        if (localDate > today)
            throw new ArgumentOutOfRangeException(nameof(localDate), "Future readings are unsupported.");
        if (!double.IsFinite(enteredValue) || enteredValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(enteredValue));

        decimal value;
        try
        {
            value = Convert.ToDecimal(enteredValue);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(enteredValue), enteredValue,
                "The reading exceeds decimal storage capacity.");
        }

        if (value > MaximumCanonicalCentimeters)
            throw new ArgumentOutOfRangeException(nameof(enteredValue), "The reading is outside technical input limits.");
        if (value * InputDecimalScale != decimal.Truncate(value * InputDecimalScale))
            throw new ArgumentOutOfRangeException(nameof(enteredValue), "At most two decimal places are supported.");

        var centimeters = enteredUnit == BodyMeasurementUnit.Inches
            ? value * CentimetersPerInch
            : value;
        if (centimeters is < MinimumCanonicalCentimeters or > MaximumCanonicalCentimeters)
            throw new ArgumentOutOfRangeException(nameof(enteredValue), "The reading is outside technical input limits.");

        return (value, centimeters);
    }

    private static bool IsMidline(BodyMeasurementSite site) =>
        site is BodyMeasurementSite.Waist or BodyMeasurementSite.Hips or BodyMeasurementSite.Chest;
}
