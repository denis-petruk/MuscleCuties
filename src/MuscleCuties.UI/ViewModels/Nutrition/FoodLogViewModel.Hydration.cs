// Water intake, presets, and reminder state.
// Second half of FoodLogViewModel; see its declaration for the full contract.

using MuscleCuties.Infrastructure.ApiClient;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UI.Services.Nutrition;

namespace MuscleCuties.UI.ViewModels.Nutrition;

/// <summary>Hydration inside the diary: the day's water, its presets and reminder times, and the legacy single-total</summary>
/// day that predates entries. Every write goes through one busy gate and one retry path.</summary>
public partial class FoodLogViewModel
{
    private readonly HydrationClient? _hydrationClient;
    private CancellationTokenSource? _hydrationScope;
    private HydrationDay? _hydrationDay;
    private HydrationPresetCatalog? _hydrationCatalog;
    private HydrationReminder? _hydrationReminder;
    private Func<CancellationToken, Task>? _pendingHydrationAction;

    [ObservableProperty] private bool _isHydrationExpanded;
    [ObservableProperty] private bool _isHydrationBusy;
    [ObservableProperty] private bool _isLegacyHydration;
    [ObservableProperty] private bool _hasHydrationRetry;
    [ObservableProperty] private string _hydrationError = string.Empty;
    [ObservableProperty] private IReadOnlyList<HydrationEntry> _hydrationEntries = [];
    [ObservableProperty] private HydrationEntry? _selectedHydrationEntry;
    [ObservableProperty] private string _hydrationEntryAmount = string.Empty;
    [ObservableProperty] private IReadOnlyList<HydrationPreset> _hydrationPresets = [];
    [ObservableProperty] private HydrationPreset? _selectedHydrationPreset;
    [ObservableProperty] private string _hydrationPresetLabel = string.Empty;
    [ObservableProperty] private string _hydrationPresetAmount = string.Empty;
    [ObservableProperty] private bool _hydrationRemindersOptedIn;
    [ObservableProperty] private TimeSpan? _hydrationQuietStart;
    [ObservableProperty] private TimeSpan? _hydrationQuietEnd;

    /// <summary>The reminder times being edited.</summary>
    public ObservableCollection<HydrationReminderTime> HydrationReminderTimes { get; } = [];

    /// <summary>Water can only be changed once a day is loaded — or on a legacy total — and while nothing else is loading,</summary>
    /// writing or waiting for a retry.</summary>
    public bool CanChangeHydration => (_hydrationDay is not null || IsLegacyHydration)
        && !IsLoading && !IsLoadError && !IsWriting && !IsHydrationBusy && !HasHydrationRetry;
    /// <summary>True when the hydration day is loaded.</summary>
    public bool CanOpenHydrationDetails => _hydrationDay is not null;
    /// <summary>True when an intake entry is selected.</summary>
    public bool CanEditHydrationEntry => SelectedHydrationEntry is not null;
    /// <summary>True when a preset is selected.</summary>
    public bool CanEditHydrationPreset => SelectedHydrationPreset is not null;
    /// <summary>A preset can move up while it is not already first and there is more than one preset.</summary>
    public bool CanMoveHydrationPresetUp => SelectedHydrationPreset is { } selected
        && HydrationPresets.Count > 1 && HydrationPresets[0].Id != selected.Id;
    /// <summary>The same rule at the other end of the list.</summary>
    public bool CanMoveHydrationPresetDown => SelectedHydrationPreset is { } selected
        && HydrationPresets.Count > 1 && HydrationPresets[^1].Id != selected.Id;
    /// <summary>True when the day has intake entries.</summary>
    public bool HasHydrationEntries => HydrationEntries.Count > 0;
    /// <summary>Reminder times are capped, so the add action stops at the limit the server accepts.</summary>
    public bool CanAddHydrationReminderTime => HydrationReminderTimes.Count < NutritionPresentationMapper.MaximumWaterReminderTimes;
    /// <summary>True when the presets are loaded.</summary>
    public bool CanConfigureHydrationPresets => _hydrationCatalog is not null;
    /// <summary>True when the reminder is loaded.</summary>
    public bool CanConfigureHydrationReminders => _hydrationReminder is not null;
    /// <summary>True when the presets were never created.</summary>
    public bool CanInitializeHydrationPresets => _hydrationCatalog is { Version: null };
    /// <summary>Whether there is a hydration message to show.</summary>
    public bool HasHydrationError => HydrationError.Length > 0;

    /// <summary>Refreshes the hydration actions when busy changes.</summary>
    partial void OnIsHydrationBusyChanged(bool value) => RefreshHydrationActions();
    /// <summary>Refreshes the hydration actions for the legacy state.</summary>
    partial void OnIsLegacyHydrationChanged(bool value) => RefreshHydrationActions();
    /// <summary>Refreshes the hydration actions when retry changes.</summary>
    partial void OnHasHydrationRetryChanged(bool value) => RefreshHydrationActions();
    /// <summary>Refreshes the error flag when the message changes.</summary>
    partial void OnHydrationErrorChanged(string value) => OnPropertyChanged(nameof(HasHydrationError));
    /// <summary>Refreshes the entries flag when the day changes.</summary>
    partial void OnHydrationEntriesChanged(IReadOnlyList<HydrationEntry> value) => OnPropertyChanged(nameof(HasHydrationEntries));

    /// <summary>Refreshes the entry actions when the selection changes.</summary>
    partial void OnSelectedHydrationEntryChanged(HydrationEntry? value)
    {
        HydrationEntryAmount = value?.Millilitres.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        OnPropertyChanged(nameof(CanEditHydrationEntry));
    }

    /// <summary>Refreshes the preset actions when the selection changes.</summary>
    partial void OnSelectedHydrationPresetChanged(HydrationPreset? value)
    {
        HydrationPresetLabel = value?.Label ?? string.Empty;
        HydrationPresetAmount = value?.Millilitres.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        OnPropertyChanged(nameof(CanEditHydrationPreset));
        RefreshHydrationPresetOrderActions();
    }

    /// <summary>Drops the loaded day, presets, reminders and any in-flight scope, so a closed page cannot write for the</summary>
    /// previous account or date.</summary>
    private void ResetHydration()
    {
        var previous = _hydrationScope;
        _hydrationScope = _isActive ? new CancellationTokenSource() : null;
        previous?.Cancel();
        previous?.Dispose();

        _pendingHydrationAction = null;
        _hydrationDay = null;
        IsHydrationExpanded = false;
        HasHydrationRetry = false;
        IsHydrationBusy = false;
        IsLegacyHydration = _hydrationClient is null;
        HydrationError = string.Empty;
        HydrationEntries = [];
        SelectedHydrationEntry = null;
        ApplyHydrationPresets(null);
        ApplyHydrationReminder(null);
        RefreshHydrationActions();
    }

    /// <summary>Re-evaluates every hydration action after the state that gates them changed.</summary>
    private void RefreshHydrationActions()
    {
        OnPropertyChanged(nameof(CanChangeHydration));
        OnPropertyChanged(nameof(CanOpenHydrationDetails));
    }

    /// <summary>Re-evaluates the two reorder actions, which depend on the selected preset's position.</summary>
    private void RefreshHydrationPresetOrderActions()
    {
        OnPropertyChanged(nameof(CanMoveHydrationPresetUp));
        OnPropertyChanged(nameof(CanMoveHydrationPresetDown));
    }

    /// <summary>Loads the day once per visit; a retry that is already offered, a write in flight or a day already loaded all</summary>
    /// short-circuit.</summary>
    private Task LoadHydrationAsync(DateOnly date)
    {
        if (_hydrationClient is null || HasHydrationRetry || IsHydrationBusy || _hydrationDay is not null)
            return Task.CompletedTask;

        return RunHydrationAsync(ct => RefreshHydrationAsync(date, ct), retryReadOnFailure: true);
    }

    /// <summary>Re-reads the day's water, keeping the user's unsaved preset and reminder drafts unless the change is what</summary>
    /// replaced them.</summary>
    private async Task RefreshHydrationAsync(DateOnly date, CancellationToken ct, bool preserveDrafts = false)
    {
        var client = _hydrationClient!;
        try
        {
            var account = await client.RequireAccountAsync(ct);
            var day = await client.ReadDayAsync(date, ct);
            await client.CheckAccountAsync(account, ct);
            RequireHydrationScope(day.AccountId, account, ct);
            if (day.Date != date)
                throw new InvalidOperationException("Hydration response date mismatch.");
            IsLegacyHydration = false;
            var entryAmount = HydrationEntryAmount;
            ApplyHydrationDay(day);
            if (preserveDrafts)
                HydrationEntryAmount = entryAmount;
            if (IsHydrationExpanded)
                await ReadHydrationDetailsAsync(account, ct, preserveDrafts);
        }
        catch (HydrationRequestException ex) when (
            ex.Status == BackendApiStatus.NotFound && _hydrationDay is null)
        {
            ct.ThrowIfCancellationRequested();
            // Older servers expose only daily-log totals.
            IsLegacyHydration = true;
        }
    }

    /// <summary>Opens or closes the hydration details.</summary>
    [RelayCommand]
    private Task ToggleHydrationDetailsAsync()
    {
        if (IsHydrationExpanded)
        {
            IsHydrationExpanded = false;
            return Task.CompletedTask;
        }

        if (_hydrationDay is not { } day || IsHydrationBusy || HasHydrationRetry)
            return Task.CompletedTask;
        IsHydrationExpanded = true;
        return RunHydrationAsync(ct => ReadHydrationDetailsAsync(day.AccountId, ct), retryReadOnFailure: true);
    }

    /// <summary>Reads the presets and reminder settings behind the expanded panel, expanding it as a side effect.</summary>
    private async Task ReadHydrationDetailsAsync(Guid account, CancellationToken ct, bool preserveDrafts = false)
    {
        var client = _hydrationClient!;
        var presets = await client.ReadPresetsAsync(ct);
        var reminder = await client.ReadReminderAsync(ct);
        await client.CheckAccountAsync(account, ct);
        RequireHydrationScope(presets.AccountId, account, ct);
        RequireHydrationScope(reminder.AccountId, account, ct);
        var presetLabel = HydrationPresetLabel;
        var presetAmount = HydrationPresetAmount;
        ApplyHydrationPresets(presets);
        if (preserveDrafts)
        {
            HydrationPresetLabel = presetLabel;
            HydrationPresetAmount = presetAmount;
        }
        if (preserveDrafts && _hydrationReminder is not null)
            _hydrationReminder = reminder;
        else
            ApplyHydrationReminder(reminder);
    }

    /// <summary>Adds water to the day.</summary>
    [RelayCommand]
    public Task AddWaterAsync(int amountMl)
    {
        if (!CanChangeHydration || !NutritionPresentationMapper.IsValidWaterVolume(amountMl))
            return Task.CompletedTask;
        return IsLegacyHydration
            ? ChangeLegacyWaterAsync(amountMl)
            : ChangeIntakeAsync(HydrationIntakeChange.Add, Guid.NewGuid(), amountMl);
    }

    /// <summary>Removes water from the day.</summary>
    [RelayCommand]
    public Task RemoveWaterAsync(int amountMl) =>
        CanChangeHydration && IsLegacyHydration && amountMl > 0
            ? ChangeLegacyWaterAsync(-amountMl)
            : Task.CompletedTask;

    /// <summary>Adds or removes water on a day that only has a single total, clamped so the total can never go negative.</summary>
    private Task ChangeLegacyWaterAsync(int difference)
    {
        var date = SelectedDate;
        var next = (int)Math.Clamp((long)WaterIntakeMl + difference, 0, int.MaxValue);
        return RunHydrationAsync(async ct =>
        {
            await _dailyLogUseCase.UpdateWaterIntakeAsync(date, next, ct);
            ct.ThrowIfCancellationRequested();
            WaterIntakeMl = next;
        });
    }

    /// <summary>Adds the selected preset's volume.</summary>
    [RelayCommand]
    private Task AddPresetWaterAsync() => SelectedHydrationPreset is { } preset
        ? AddWaterAsync(preset.Millilitres)
        : Task.CompletedTask;

    /// <summary>Corrects the selected entry.</summary>
    [RelayCommand]
    private Task CorrectHydrationEntryAsync() =>
        CanChangeHydration && SelectedHydrationEntry is { } entry && TryReadWaterAmount(HydrationEntryAmount, out var amount)
            ? ChangeIntakeAsync(HydrationIntakeChange.Correct, entry.Id, amount)
            : Task.CompletedTask;

    /// <summary>Undoes the selected entry.</summary>
    [RelayCommand]
    private Task UndoHydrationEntryAsync() => CanChangeHydration && SelectedHydrationEntry is { } entry
        ? ChangeIntakeAsync(HydrationIntakeChange.Undo, entry.Id, 0)
        : Task.CompletedTask;

    /// <summary>Applies one intake change to the day's entries under the busy gate, so two taps cannot interleave.</summary>
    private Task ChangeIntakeAsync(HydrationIntakeChange change, Guid entryId, int millilitres)
    {
        if (_hydrationDay is not { } day)
            return Task.CompletedTask;

        var operationId = Guid.NewGuid();
        return RunHydrationAsync(async ct =>
        {
            var client = _hydrationClient!;
            await client.CheckAccountAsync(day.AccountId, ct);
            var updated = await client.ChangeDayAsync(day, operationId, entryId, change, millilitres, ct);
            RequireHydrationScope(updated.AccountId, day.AccountId, ct);
            if (updated.Date != day.Date)
                throw new InvalidOperationException("Hydration response date mismatch.");
            ApplyHydrationDay(updated);
        });
    }

    /// <summary>Starts a new preset.</summary>
    [RelayCommand]
    private void NewHydrationPreset()
    {
        SelectedHydrationPreset = null;
        HydrationPresetLabel = string.Empty;
        HydrationPresetAmount = string.Empty;
    }

    /// <summary>Creates the default presets.</summary>
    [RelayCommand]
    private Task InitializeHydrationPresetsAsync() =>
        CanInitializeHydrationPresets
            ? ChangePresetsAsync((_, ct) => _hydrationClient!.InitializePresetsAsync(ct))
            : Task.CompletedTask;

    /// <summary>Saves the edited preset.</summary>
    [RelayCommand]
    private Task SaveHydrationPresetAsync()
    {
        if (!CanChangeHydration || !TryReadWaterAmount(HydrationPresetAmount, out var amount))
            return Task.CompletedTask;
        var label = HydrationPresetLabel.Trim();
        var selectedId = SelectedHydrationPreset?.Id;
        if (label.Length == 0 || label.Length > NutritionPresentationMapper.MaximumWaterPresetLabelLength
            || (selectedId is null && HydrationPresets.Count >= NutritionPresentationMapper.MaximumWaterPresetCount)
            || HydrationPresets.Any(preset => preset.Id != selectedId
                && string.Equals(preset.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            HydrationError = UiTextCatalog.Get("Nutrition_Hydration_InvalidPreset");
            return Task.CompletedTask;
        }

        var operationId = Guid.NewGuid();
        return ChangePresetsAsync((version, ct) =>
            _hydrationClient!.SavePresetAsync(version, selectedId, label, amount, operationId, ct));
    }

    /// <summary>Deletes the edited preset.</summary>
    [RelayCommand]
    private async Task DeleteHydrationPresetAsync()
    {
        if (SelectedHydrationPreset is not { } preset)
            return;

        var confirmed = await ConfirmAsync(
            UiTextCatalog.Get("Nutrition_Hydration_DeletePreset"),
            UiTextCatalog.Get("Nutrition_FoodLog_DeleteItemMessage"),
            UiTextCatalog.Get("Common_Delete"),
            UiTextCatalog.Get("Common_Cancel"));
        if (!confirmed)
            return;

        var operationId = Guid.NewGuid();
        await ChangePresetsAsync((version, ct) =>
            _hydrationClient!.DeletePresetAsync(version, preset.Id, operationId, ct));
    }

    /// <summary>Moves the preset up the order.</summary>
    [RelayCommand]
    private Task MoveHydrationPresetUpAsync() => MoveHydrationPresetAsync(-1);

    /// <summary>Moves the preset down the order.</summary>
    [RelayCommand]
    private Task MoveHydrationPresetDownAsync() => MoveHydrationPresetAsync(1);

    /// <summary>Swaps the selected preset with its neighbour and sends the new order.</summary>
    private Task MoveHydrationPresetAsync(int offset)
    {
        if (SelectedHydrationPreset is not { } preset)
            return Task.CompletedTask;

        var order = HydrationPresets.Select(item => item.Id).ToArray();
        var index = Array.IndexOf(order, preset.Id);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= order.Length)
            return Task.CompletedTask;

        (order[index], order[target]) = (order[target], order[index]);
        var operationId = Guid.NewGuid();
        return ChangePresetsAsync((version, ct) => _hydrationClient!.ReorderPresetsAsync(version, order, operationId, ct));
    }

    /// <summary>Runs one preset change with the catalog's version, replacing the local catalog with the server's answer.</summary>
    private Task ChangePresetsAsync(Func<string?, CancellationToken, Task<HydrationPresetCatalog>> change)
    {
        if (!CanChangeHydration || _hydrationCatalog is not { } catalog)
            return Task.CompletedTask;

        return RunHydrationAsync(async ct =>
        {
            await _hydrationClient!.CheckAccountAsync(catalog.AccountId, ct);
            var updated = await change(catalog.Version, ct);
            RequireHydrationScope(updated.AccountId, catalog.AccountId, ct);
            ApplyHydrationPresets(updated);
            if (SelectedHydrationPreset is null)
                NewHydrationPreset();
            ShowStatus(UiTextCatalog.Get("Nutrition_Hydration_PresetsSaved"));
        });
    }

    /// <summary>Adds a reminder time.</summary>
    [RelayCommand]
    private void AddHydrationReminderTime()
    {
        if (CanAddHydrationReminderTime)
        {
            HydrationReminderTimes.Add(new());
            OnPropertyChanged(nameof(CanAddHydrationReminderTime));
        }
    }

    /// <summary>Removes a reminder time.</summary>
    [RelayCommand]
    private void RemoveHydrationReminderTime(HydrationReminderTime? reminderTime)
    {
        if (reminderTime is not null && HydrationReminderTimes.Remove(reminderTime))
            OnPropertyChanged(nameof(CanAddHydrationReminderTime));
    }

    /// <summary>Saves the reminder times.</summary>
    [RelayCommand]
    private Task SaveHydrationReminderAsync()
    {
        if (!CanChangeHydration || _hydrationReminder is not { } reminder)
            return Task.CompletedTask;

        var optedIn = HydrationRemindersOptedIn;
        var times = HydrationReminderTimes.Where(item => item.Time.HasValue)
            .Select(item => TimeOnly.FromTimeSpan(item.Time!.Value)).ToArray();
        TimeOnly? quietStart = HydrationQuietStart is { } start ? TimeOnly.FromTimeSpan(start) : null;
        TimeOnly? quietEnd = HydrationQuietEnd is { } end ? TimeOnly.FromTimeSpan(end) : null;
        if (optedIn && (quietStart is null || quietEnd is null || times.Length != HydrationReminderTimes.Count
            || !NutritionPresentationMapper.IsValidWaterReminder(times, quietStart.Value, quietEnd.Value)))
        {
            HydrationError = UiTextCatalog.Get("Nutrition_Hydration_InvalidReminder");
            return Task.CompletedTask;
        }

        var operationId = Guid.NewGuid();
        return RunHydrationAsync(async ct =>
        {
            var client = _hydrationClient!;
            await client.CheckAccountAsync(reminder.AccountId, ct);
            var updated = await client.SaveReminderAsync(reminder.Version, optedIn, times, quietStart, quietEnd, operationId, ct);
            RequireHydrationScope(updated.AccountId, reminder.AccountId, ct);
            ApplyHydrationReminder(updated);
            ShowStatus(UiTextCatalog.Get("Nutrition_Hydration_ReminderPreferencesSaved"));
        });
    }

    /// <summary>Retries the failed hydration action.</summary>
    [RelayCommand]
    private Task RetryHydrationAsync() => _pendingHydrationAction is { } retry
        ? RunHydrationAsync(retry, retryReadOnFailure: true)
        : Task.CompletedTask;

    /// <summary>The one hydration write path: marks the panel busy, keeps the page's own token so a close cancels it, and</summary>
    /// records a read that can be retried.</summary>
    private async Task RunHydrationAsync(Func<CancellationToken, Task> action, bool retryReadOnFailure = false)
    {
        if (IsHydrationBusy || _hydrationScope is not { } scope)
            return;

        var ct = scope.Token;
        IsHydrationBusy = true;
        HydrationError = string.Empty;
        _pendingHydrationAction = null;
        HasHydrationRetry = false;
        try
        {
            await action(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Navigation or date selection invalidated this operation's UI scope.
        }
        catch (HydrationRequestException ex) when (!ct.IsCancellationRequested && ex.Status == BackendApiStatus.Conflict)
        {
            if (ex.Notice.MessageKey == "Nutrition_Hydration_LegacyTotalConflict")
            {
                HydrationError = ex.Notice.MessageText;
                return;
            }

            var date = SelectedDate;
            _pendingHydrationAction = next => RefreshHydrationAsync(date, next, preserveDrafts: true);
            HasHydrationRetry = true;
            HydrationError = UiTextCatalog.Get("Nutrition_Hydration_Conflict");
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested)
                return;

            var notice = ex is HydrationRequestException failure
                ? failure.Notice
                : ApiFailurePresenter.FromException(ex, "Nutrition_Hydration_RequestFailed");
            if (notice.IsRetryable || (retryReadOnFailure && notice.Action == ApiFailureAction.None))
            {
                _pendingHydrationAction = action;
                HasHydrationRetry = true;
            }
            HydrationError = notice.MessageText;
        }
        finally
        {
            if (ReferenceEquals(_hydrationScope, scope))
                IsHydrationBusy = false;
        }
    }

    /// <summary>Reads a typed water amount, refusing anything outside the range a drink can be.</summary>
    private bool TryReadWaterAmount(string text, out int amount)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out amount)
            && NutritionPresentationMapper.IsValidWaterVolume(amount))
            return true;

        HydrationError = UiTextCatalog.Get("Nutrition_Hydration_InvalidAmount");
        return false;
    }

    /// <summary>Rejects a hydration answer that arrived for another account or after the page was closed, so one</summary>
    /// account's water can never be written into another's.</summary>
    private static void RequireHydrationScope(Guid returnedAccount, Guid account, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (returnedAccount != account)
            throw new UnauthorizedAccessException();
    }

    /// <summary>Takes the day's water and entries from the server and republishes the actions that depend on them.</summary>
    private void ApplyHydrationDay(HydrationDay day)
    {
        var selected = SelectedHydrationEntry?.Id;
        _hydrationDay = day;
        WaterIntakeMl = day.TotalMillilitres;
        HydrationEntries = day.Entries;
        SelectedHydrationEntry = HydrationEntries.FirstOrDefault(entry => entry.Id == selected);
        RefreshHydrationActions();
    }

    /// <summary>Replaces the preset list from a catalog answer, or clears it when there is none.</summary>
    private void ApplyHydrationPresets(HydrationPresetCatalog? catalog)
    {
        var selected = SelectedHydrationPreset?.Id;
        _hydrationCatalog = catalog;
        HydrationPresets = catalog?.Presets ?? [];
        SelectedHydrationPreset = HydrationPresets.FirstOrDefault(preset => preset.Id == selected);
        OnPropertyChanged(nameof(CanConfigureHydrationPresets));
        OnPropertyChanged(nameof(CanInitializeHydrationPresets));
        RefreshHydrationPresetOrderActions();
    }

    /// <summary>Replaces the reminder settings from a server answer, or clears them when there is none.</summary>
    private void ApplyHydrationReminder(HydrationReminder? reminder)
    {
        _hydrationReminder = reminder;
        HydrationRemindersOptedIn = reminder?.OptedIn ?? false;
        HydrationQuietStart = reminder?.QuietStart?.ToTimeSpan();
        HydrationQuietEnd = reminder?.QuietEnd?.ToTimeSpan();
        HydrationReminderTimes.Clear();
        foreach (var time in reminder?.LocalTimes ?? [])
            HydrationReminderTimes.Add(new() { Time = time.ToTimeSpan() });
        OnPropertyChanged(nameof(CanConfigureHydrationReminders));
        OnPropertyChanged(nameof(CanAddHydrationReminderTime));
    }
}
