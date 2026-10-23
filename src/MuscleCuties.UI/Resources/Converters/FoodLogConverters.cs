using System.Globalization;
using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Resources.Converters;

public class BoolToSelectionStrokeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        if (Application.Current?.Resources.TryGetValue("Primary", out var primary) == true && isSelected)
            return (Color)primary;

        if (Application.Current?.Resources.TryGetValue("Gray300", out var gray) == true)
            return (Color)gray;

        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolToSelectionBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        if (!isSelected)
            return Colors.Transparent;

        if (Application.Current?.Resources.TryGetValue("AccentSurface", out var accent) == true)
            return (Color)accent;

        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolToEditTitleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "Edit Food Entry" : "Log Food Item";
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
        return value is true ? "Warmup" : "Work";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
