namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>The two calendar dates explicitly selected for a diary copy.</summary>
public sealed class DiaryCopyDates
{
    /// <summary>Takes the two dates a copy spans.</summary>
    private DiaryCopyDates(DateOnly sourceDate, DateOnly targetDate)
    {
        SourceDate = sourceDate;
        TargetDate = targetDate;
    }

    /// <summary>Day the entries are copied from.</summary>
    public DateOnly SourceDate { get; }
    /// <summary>Day the entries are copied to.</summary>
    public DateOnly TargetDate { get; }

    /// <summary>Validates the pair, refusing a copy onto the same day or into the future.</summary>
    public static DiaryCopyDates Create(DateOnly sourceDate, DateOnly targetDate)
    {
        if (sourceDate == default)
            throw new ArgumentException("Choose a source date for a diary copy.", nameof(sourceDate));
        if (targetDate == default)
            throw new ArgumentException("Choose a target date for a diary copy.", nameof(targetDate));
        if (sourceDate == targetDate)
            throw new ArgumentException("Choose a different target date for a diary copy.", nameof(targetDate));

        return new DiaryCopyDates(sourceDate, targetDate);
    }
}
