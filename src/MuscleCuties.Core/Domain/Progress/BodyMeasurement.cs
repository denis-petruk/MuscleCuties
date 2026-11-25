namespace MuscleCuties.Core.Domain.Progress;

/// <summary>Where a measurement was taken, which decides the plausible range it must fall in.</summary>
public enum BodyMeasurementSite
{
    Waist,
    Hips,
    Chest,
    UpperArm,
    Thigh,
    Calf
}

/// <summary>Which side was measured, so left and right are not averaged together.</summary>
public enum BodyMeasurementSide
{
    None,
    Left,
    Right
}

/// <summary>The unit she entered, kept so the stored value is never re-interpreted.</summary>
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

    /// <summary>Rebuilds a measurement from stored state without re-running the creation checks.</summary>
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

    /// <summary>Identity of the measurement.</summary>
    public Guid Id { get; }
    /// <summary>Where the measurement was taken.</summary>
    public BodyMeasurementSite Site { get; private set; }
    /// <summary>Which side was measured, absent for a site with only one.</summary>
    public BodyMeasurementSide Side { get; private set; }
    /// <summary>The day she measured, in her local date rather than a timestamp.</summary>
    public DateOnly LocalDate { get; private set; }
    /// <summary>The figure exactly as she typed it, kept so a unit change does not rewrite history.</summary>
    public decimal EnteredValue { get; private set; }
    /// <summary>The unit that figure was given in.</summary>
    public BodyMeasurementUnit EnteredUnit { get; private set; }
    /// <summary>The measurement converted to centimetres, which is what comparisons use.</summary>
    public decimal ValueCentimeters { get; private set; }
    /// <summary>The version a correction must present, so a stale device cannot overwrite a newer reading.</summary>
    public int Version { get; private set; }
    /// <summary>Whether the measurement is withdrawn; a deleted row is hidden but kept for the correction history.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Refuses a write with no command key, which would make the change non-idempotent.</summary>
    public static void RequireCommandId(Guid commandId)
    {
        if (commandId == Guid.Empty)
            throw new ArgumentException("A stable command ID is required for safe retries.", nameof(commandId));
    }

    /// <summary>Refuses a history range that is inverted or wider than the allowed window.</summary>
    public static void RequireHistoryRange(DateOnly from, DateOnly through, DateOnly today)
    {
        if (from > through || through > today)
            throw new ArgumentOutOfRangeException(nameof(from), "History range must end no later than today.");
    }

    /// <summary>Records a new measurement after checking the site, side and value.</summary>
    public static BodyMeasurement Create(Guid id, BodyMeasurementSite site, BodyMeasurementSide side,
        DateOnly localDate, double enteredValue, BodyMeasurementUnit enteredUnit, DateOnly today)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Measurement ID is required.", nameof(id));

        var (value, centimeters) = Validate(site, side, localDate, enteredValue, enteredUnit, today);
        return new BodyMeasurement(id, site, side, localDate, value, enteredUnit, centimeters);
    }

    /// <summary>Rehydrates a measurement from storage, keeping its version and timestamps.</summary>
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

    /// <summary>Rewrites the measurement's value under the expected version.</summary>
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

    /// <summary>Withdraws the measurement, advancing its version so a stale correction is refused.</summary>
    public void Delete(int expectedVersion)
    {
        EnsureCurrentVersion(expectedVersion);
        var nextVersion = checked(Version + 1);
        IsDeleted = true;
        Version = nextVersion;
    }

    /// <summary>True when both measurements record the same site on the same day, so one replaces the other.</summary>
    public bool OccupiesSameDailySlotAs(BodyMeasurement other) =>
        other is not null && !IsDeleted && !other.IsDeleted && Id != other.Id &&
        Site == other.Site && Side == other.Side && LocalDate == other.LocalDate;

    /// <summary>Refuses a correction based on an older version.</summary>
    private void EnsureCurrentVersion(int expectedVersion)
    {
        if (IsDeleted)
            throw new InvalidOperationException("A deleted measurement cannot be changed.");
        if (expectedVersion != Version)
            throw new InvalidOperationException("The measurement version is stale.");
    }

    /// <summary>Checks a measurement against its site's allowed range and converts it to centimetres.</summary>
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

    /// <summary>True when the site sits on the body's midline, where no side applies.</summary>
    private static bool IsMidline(BodyMeasurementSite site) =>
        site is BodyMeasurementSite.Waist or BodyMeasurementSite.Hips or BodyMeasurementSite.Chest;
}
