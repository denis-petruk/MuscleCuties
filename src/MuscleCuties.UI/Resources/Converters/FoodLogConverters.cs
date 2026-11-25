using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Resources.Converters;

/// <summary>Title for the food entry sheet: "edit" when an entry is open, "log" when adding one.</summary>
public class BoolToEditTitleConverter : IValueConverter
{
    /// <summary>Picks the localized title from whether an existing entry is being edited.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? UiTextCatalog.Get("Nutrition_FoodLog_EditEntryTitle")
            : UiTextCatalog.Get("Nutrition_FoodLog_LogEntryTitle");
    }

    /// <summary>Not supported: the title is display only.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Labels a set row as a warm-up or a working set.</summary>
public class BoolToWarmupTextConverter : IValueConverter
{
    /// <summary>Warm-up wording for a warm-up row, working-set wording otherwise.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? UiTextCatalog.Get("Workout_History_WarmupSet")
            : UiTextCatalog.Get("Workout_History_WorkingSet");
    }

    /// <summary>Not supported: the label is display only.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
