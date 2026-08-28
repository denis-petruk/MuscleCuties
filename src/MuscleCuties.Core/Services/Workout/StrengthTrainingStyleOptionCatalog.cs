using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.UI.Workout;

namespace MuscleCuties.Core.Services.Workout;

public static class StrengthTrainingStyleOptionCatalog
{
    public static ObservableCollection<StrengthTrainingStyleOptionItem> Build(StrengthTrainingStyle selectedStyle) =>
    [
        Option(
            StrengthTrainingStyle.ComfortableModerate,
            "Comfortable and moderate",
            "Classic hypertrophy work with steady volume, cleaner recovery, and fewer spike days.",
            selectedStyle),
        Option(
            StrengthTrainingStyle.ExpressHard,
            "Express and hard",
            "Shorter strength sessions with lower volume and higher intent when your phase and energy allow it.",
            selectedStyle)
    ];

    private static StrengthTrainingStyleOptionItem Option(
        StrengthTrainingStyle style,
        string title,
        string subtitle,
        StrengthTrainingStyle selectedStyle) =>
        new()
        {
            Style = style,
            Title = title,
            Subtitle = subtitle,
            IsSelected = style == selectedStyle
        };
}
