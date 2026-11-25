
namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>One day of water intake: the running total, the entries behind it, and the version they were</summary>
/// read at.</summary>
public sealed record HydrationDay(Guid AccountId, DateOnly Date, int TotalMillilitres,
    IReadOnlyList<HydrationEntry> Entries, string? Version);

/// <summary>One logged drink within the day.</summary>
public sealed record HydrationEntry(Guid Id, int Millilitres, int Number)
{
    /// <summary>The entry as shown in the day's list: its position and volume.</summary>
    public string DisplayText => UiTextCatalog.Format("Nutrition_Hydration_Entry", Number, Millilitres);
}

/// <summary>A saved quick-add volume the person can log with one tap.</summary>
public sealed record HydrationPreset(Guid Id, string Label, int Millilitres)
{
    /// <summary>The preset as shown on its button: label and volume.</summary>
    public string DisplayText => UiTextCatalog.Format("Nutrition_Hydration_Preset", Label, Millilitres);
}

/// <summary>The account's presets with the version the client must send back when reordering them.</summary>
public sealed record HydrationPresetCatalog(Guid AccountId, string? Version, IReadOnlyList<HydrationPreset> Presets);

/// <summary>Reminder settings: whether they are on, the times, and the quiet window they must avoid.</summary>
public sealed record HydrationReminder(Guid AccountId, string? Version, bool OptedIn,
    IReadOnlyList<TimeOnly> LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd);

/// <summary>The three edits a day's intake accepts; <c>Undo</c> removes the last entry rather than setting a</summary>
/// value.</summary>
public enum HydrationIntakeChange { Add, Correct, Undo }

/// <summary>One editable reminder time in the settings list.</summary>
public sealed partial class HydrationReminderTime : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private TimeSpan? _time;
}
