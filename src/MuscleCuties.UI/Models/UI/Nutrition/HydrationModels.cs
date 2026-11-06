using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Models.UI.Nutrition;

public sealed record HydrationDay(Guid AccountId, DateOnly Date, int TotalMillilitres,
    IReadOnlyList<HydrationEntry> Entries, string? Version);

public sealed record HydrationEntry(Guid Id, int Millilitres, int Number)
{
    public string DisplayText => UiTextCatalog.Format("Nutrition_Hydration_Entry", Number, Millilitres);
}

public sealed record HydrationPreset(Guid Id, string Label, int Millilitres)
{
    public string DisplayText => UiTextCatalog.Format("Nutrition_Hydration_Preset", Label, Millilitres);
}

public sealed record HydrationPresetCatalog(Guid AccountId, string? Version, IReadOnlyList<HydrationPreset> Presets);

public sealed record HydrationReminder(Guid AccountId, string? Version, bool OptedIn,
    IReadOnlyList<TimeOnly> LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd);

public enum HydrationIntakeChange { Add, Correct, Undo }

public sealed partial class HydrationReminderTime : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private TimeSpan? _time;
}
