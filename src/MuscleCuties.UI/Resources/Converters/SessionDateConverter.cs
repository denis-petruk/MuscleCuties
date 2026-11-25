using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Formats the account-local session date.</summary>
public sealed class SessionDateConverter : IValueConverter
{
    /// <summary>Formats the session date for display.</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is WorkoutArchiveSummaryDto session
            ? DisplayFormat.DateWithWeekday(session.AccountLocalStartedAt ?? session.StartedAt)
            : string.Empty;

    /// <summary>Rejects the back conversion; the date only formats for display.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
