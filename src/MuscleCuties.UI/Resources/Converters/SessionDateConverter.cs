using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Formats the account-local session date.</summary>
public sealed class SessionDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is WorkoutArchiveSummaryDto session
            ? DisplayFormat.DateWithWeekday(session.AccountLocalStartedAt ?? session.StartedAt)
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
