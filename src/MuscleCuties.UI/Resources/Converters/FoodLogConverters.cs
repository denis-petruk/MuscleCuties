using System.Globalization;
using Microsoft.Maui.Controls;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Resources.Converters;

public class BoolToEditTitleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? UiTextCatalog.Get("Nutrition_FoodLog_EditEntryTitle")
            : UiTextCatalog.Get("Nutrition_FoodLog_LogEntryTitle");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolToWarmupTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? UiTextCatalog.Get("Workout_History_WarmupSet")
            : UiTextCatalog.Get("Workout_History_WorkingSet");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
