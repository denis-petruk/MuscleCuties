using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UI.Services.Api;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>
///     Archive state: completed sessions grouped by week, with their detail.
/// </summary>
public partial class WorkoutHistoryViewModel : ObservableObject
{
    private const int PageSize = 25;
    private const double SecondsPerMinute = 60.0;
    private const double KilogramRoundingThreshold = 1000.0;
    private const int StatusMessageVisibilityMilliseconds = 3000;
    private readonly Func<Task>? _navigateBackAsync;
    private readonly IWorkoutArchiveUseCase _workoutArchiveUseCase;

    [ObservableProperty] private string _currentFilterTitle = UiTextCatalog.Get("Workout_History_AllMonths");

    private int _currentOffset;

    [ObservableProperty] private string _detailDistanceFormatted = string.Empty;

    [ObservableProperty] private string _detailDurationFormatted = string.Empty;

    [ObservableProperty] private string _detailKindLabel = string.Empty;

    [ObservableProperty] private string _detailStartedFormatted = string.Empty;

    [ObservableProperty] private string _detailVolumeFormatted = string.Empty;

    [ObservableProperty] private string _editingNotes = string.Empty;

    [ObservableProperty] private int? _editingRpe;

    [ObservableProperty] private string _editingRpeText = string.Empty;

    [ObservableProperty] private string _rpeValidationError = string.Empty;

    [ObservableProperty] private string _errorMessage = string.Empty;

    private int _filterRequestId;
    private int _statusVersion;

    [ObservableProperty] private bool _hasDetailDistance;

    [ObservableProperty] private bool _hasMoreSessions;

    [ObservableProperty] private bool _hasNoSessions;

    // Session Detail Sheet state
    [ObservableProperty] private bool _isDetailSheetVisible;

    [ObservableProperty] private bool _isEndReached;

    [ObservableProperty] private bool _isError;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _isLoadingMore;

    [ObservableProperty] private bool _isSavingNotes;

    [ObservableProperty] private bool _isStatusVisible;

    [ObservableProperty] private string _lifetimeVolumeFormatted =
        UiTextCatalog.Format("Workout_History_ValueKgFormat", "0");

    [ObservableProperty] private double _lifetimeVolumeKg;

    [ObservableProperty] private MonthFilterItem? _selectedMonth;

    [ObservableProperty] private WorkoutArchiveDetailDto? _selectedSessionDetail;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private int _totalSessionsCount;

    /// <summary>Takes the archive use case and the back navigation callback.</summary>
    public WorkoutHistoryViewModel(
        IWorkoutArchiveUseCase workoutArchiveUseCase,
        Func<Task>? navigateBackAsync = null)
    {
        _workoutArchiveUseCase = workoutArchiveUseCase;
        _navigateBackAsync = navigateBackAsync;
    }

    /// <summary>The history sessions.</summary>
    public ObservableCollection<WorkoutArchiveSummaryDto> Sessions { get; } = [];

    /// <summary>The recorded effort as text, or "not recorded" when the session has none.</summary>
    public string DetailRpeFormatted => EditingRpe is { } rpe
        ? UiTextCatalog.Format("Workout_History_RpeValueFormat", rpe)
        : UiTextCatalog.Get("Workout_History_NotRecorded");

    /// <summary>The months the history can be filtered to.</summary>
    public ObservableCollection<MonthFilterItem> AvailableMonths { get; } = [];

    /// <summary>The exercise groups of the open session.</summary>
    public ObservableCollection<ExerciseSetsUiGroup> ExerciseGroups { get; } = [];

    /// <summary>Refreshes the edit state when the effort changes.</summary>
    partial void OnEditingRpeChanged(int? value)
    {
        OnPropertyChanged(nameof(DetailRpeFormatted));
    }

    /// <summary>Loads the history the page opens with.</summary>
    public async Task InitializeAsync()
    {
        await LoadHistoryAsync();
    }

    /// <summary>Loads the history.</summary>
    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var requestId = ++_filterRequestId;
        IsLoading = true;
        IsError = false;
        ErrorMessage = string.Empty;
        _currentOffset = 0;
        try
        {
            // Populate monthly filter items from available dates
            if (AvailableMonths.Count == 0)
            {
                var allItem = new MonthFilterItem(
                    0, 0, UiTextCatalog.Get("Workout_History_AllMonths"), true) { IsSelected = true };
                AvailableMonths.Add(allItem);
                SelectedMonth = allItem;

                var distinctMonths = (await _workoutArchiveUseCase.GetAvailableHistoryMonthsAsync()).ToList();
                if (requestId != _filterRequestId) return;

                foreach (var option in distinctMonths)
                {
                    var dt = new DateTime(option.Year, option.Month, 1);
                    AvailableMonths.Add(new MonthFilterItem(option.Year, option.Month,
                        DisplayFormat.MonthYear(dt)));
                }
            }

            if (SelectedMonth is not null && !SelectedMonth.IsAllMonths)
            {
                await LoadFilteredHistoryAsync();
                return;
            }

            var totalCount = await _workoutArchiveUseCase.GetTotalSessionsCountAsync();
            if (requestId != _filterRequestId) return;
            TotalSessionsCount = totalCount;

            var history = await _workoutArchiveUseCase.GetWorkoutHistoryAsync(PageSize, 0);
            if (requestId != _filterRequestId) return;

            ApplySessions(history);
            HasMoreSessions = history.Count >= PageSize &&
                              (TotalSessionsCount == 0 || Sessions.Count < TotalSessionsCount);
            IsEndReached = !HasMoreSessions;
            _currentOffset = history.Count;
        }
        catch (Exception ex)
        {
            if (requestId == _filterRequestId)
            {
                ShowFailure(ex, "Workout_History_Error_Load", markLoadError: true);
            }
        }
        finally
        {
            if (requestId == _filterRequestId) IsLoading = false;
        }
    }

    /// <summary>Filters the history to a month.</summary>
    [RelayCommand]
    public async Task SelectMonthAsync(MonthFilterItem? month)
    {
        if (month is null || month == SelectedMonth)
            return;

        foreach (var m in AvailableMonths)
            m.IsSelected = m == month;

        SelectedMonth = month;
        CurrentFilterTitle = month.DisplayName;

        await LoadFilteredHistoryAsync();
    }

    /// <summary>Shows the history unfiltered.</summary>
    [RelayCommand]
    public async Task ShowAllMonthsAsync()
    {
        var all = AvailableMonths.FirstOrDefault(m => m.IsAllMonths);
        if (all is not null)
            await SelectMonthAsync(all);
    }

    /// <summary>Reloads the list for the selected month, tagging the request so a slower earlier response cannot</summary>
    /// replace the newer month's rows.</summary>
    private async Task LoadFilteredHistoryAsync()
    {
        var requestId = ++_filterRequestId;
        IsLoading = true;
        IsError = false;
        ErrorMessage = string.Empty;
        _currentOffset = 0;
        try
        {
            IReadOnlyList<WorkoutArchiveSummaryDto> sessions;
            if (SelectedMonth is null || SelectedMonth.IsAllMonths)
            {
                sessions = await _workoutArchiveUseCase.GetWorkoutHistoryAsync(PageSize, 0);
                var total = await _workoutArchiveUseCase.GetTotalSessionsCountAsync();
                if (requestId != _filterRequestId) return;
                TotalSessionsCount = total;
            }
            else
            {
                sessions = await _workoutArchiveUseCase.GetWorkoutHistoryByMonthAsync(
                    SelectedMonth.Year, SelectedMonth.Month, PageSize);
                if (requestId != _filterRequestId) return;
                TotalSessionsCount = await _workoutArchiveUseCase.GetWorkoutHistoryCountByMonthAsync(
                    SelectedMonth.Year, SelectedMonth.Month);
            }

            if (requestId != _filterRequestId) return;

            ApplySessions(sessions);
            HasMoreSessions = sessions.Count >= PageSize &&
                              (TotalSessionsCount == 0 || Sessions.Count < TotalSessionsCount);
            IsEndReached = !HasMoreSessions;
            _currentOffset = sessions.Count;
        }
        catch (Exception ex)
        {
            if (requestId == _filterRequestId)
            {
                ShowFailure(ex, "Workout_History_Error_Filter", markLoadError: true);
            }
        }
        finally
        {
            if (requestId == _filterRequestId) IsLoading = false;
        }
    }

    /// <summary>Loads the next page of history.</summary>
    [RelayCommand]
    public async Task LoadNextPageAsync()
    {
        if (IsLoading || IsLoadingMore || !HasMoreSessions)
            return;

        var requestId = _filterRequestId;
        IsLoadingMore = true;
        IsError = false;
        try
        {
            IReadOnlyList<WorkoutArchiveSummaryDto> nextSessions;
            if (SelectedMonth is null || SelectedMonth.IsAllMonths)
                nextSessions = await _workoutArchiveUseCase.GetWorkoutHistoryAsync(PageSize, _currentOffset);
            else
                nextSessions = await _workoutArchiveUseCase.GetWorkoutHistoryByMonthAsync(
                    SelectedMonth.Year, SelectedMonth.Month, PageSize, _currentOffset);

            if (requestId != _filterRequestId) return;

            ApplySessions(nextSessions, true);
            HasMoreSessions = nextSessions.Count >= PageSize &&
                              (TotalSessionsCount == 0 || Sessions.Count < TotalSessionsCount);
            IsEndReached = !HasMoreSessions;
            _currentOffset += nextSessions.Count;
        }
        catch (Exception ex)
        {
            if (requestId == _filterRequestId)
            {
                ShowFailure(ex, "Workout_History_Error_LoadMore", markLoadError: true);
            }
        }
        finally
        {
            if (requestId == _filterRequestId) IsLoadingMore = false;
        }
    }

    /// <summary>Puts the loaded sessions into the list, either replacing it or appending for the next page, and keeps the</summary>
    /// lifetime volume from being counted twice on append.</summary>
    private void ApplySessions(IReadOnlyList<WorkoutArchiveSummaryDto> history, bool isAppend = false)
    {
        var currentTotal = isAppend ? LifetimeVolumeKg : 0.0;
        if (!isAppend) Sessions.Clear();

        foreach (var session in history)
            if (!Sessions.Any(s => s.Id == session.Id))
            {
                Sessions.Add(session);
                currentTotal += session.TotalVolumeKg;
            }

        LifetimeVolumeKg = Math.Round(currentTotal, 0);
        LifetimeVolumeFormatted = FormatKilograms(LifetimeVolumeKg);

        HasNoSessions = Sessions.Count == 0;
    }

    /// <summary>Opens one session's detail.</summary>
    [RelayCommand]
    public async Task OpenSessionDetailAsync(WorkoutArchiveSummaryDto summary)
    {
        IsLoading = true;
        try
        {
            var detail = await _workoutArchiveUseCase.GetWorkoutSessionDetailAsync(summary.Id);
            SelectedSessionDetail = detail;

            DetailDurationFormatted = detail.TotalDurationSeconds > 0
                ? UiTextCatalog.Format(
                    "Workout_History_DurationMinutesFormat",
                    Math.Round(detail.TotalDurationSeconds / SecondsPerMinute))
                : UiTextCatalog.Get("Workout_History_NotRecorded");
            HasDetailDistance = detail.DistanceKm is not null;
            DetailDistanceFormatted = detail.DistanceKm is { } distanceKm
                ? UiTextCatalog.Format("Workout_History_DistanceFormat", distanceKm)
                : string.Empty;
            var startedAt = detail.AccountLocalStartedAt ?? detail.StartedAt;
            var startedText = detail.AccountLocalStartedAt is null && startedAt.TimeOfDay == TimeSpan.Zero
                ? DisplayFormat.DateWithWeekday(startedAt)
                : DisplayFormat.DateWithWeekdayStamp(startedAt);
            DetailStartedFormatted = detail.TimePrecision == "legacyUnknown"
                ? UiTextCatalog.Format("Workout_History_TimeZoneUnknownFormat", startedText)
                : startedText;
            DetailVolumeFormatted = detail.DetailKind == "aggregateOnly" && detail.TotalVolumeKg <= 0
                ? UiTextCatalog.Get("Workout_History_NotRecorded")
                : FormatKilograms(detail.TotalVolumeKg);
            DetailKindLabel = detail.DetailKind switch
            {
                "aggregateOnly" => UiTextCatalog.Get("Workout_History_AggregateOnlyNote"),
                "noSetDetails" => UiTextCatalog.Get("Workout_History_NoSetDetailsNote"),
                _ => string.Empty
            };
            EditingNotes = detail.Notes ?? string.Empty;
            EditingRpe = detail.RpeRating;
            EditingRpeText = detail.RpeRating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            RpeValidationError = string.Empty;

            ExerciseGroups.Clear();
            var grouped = detail.Sets
                .GroupBy(s => s.ExerciseName, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in grouped)
            {
                var groupUi = new ExerciseSetsUiGroup(UiTextCatalog.TranslateRuntimeText(grp.Key));
                foreach (var set in grp.OrderBy(s => s.SetNumber))
                {
                    groupUi.Sets.Add(set);
                    groupUi.EditableSets.Add(new EditableExerciseSetItem(
                        set.Id,
                        set.WorkoutSessionLogId,
                        set.ExerciseName,
                        set.SetNumber,
                        set.WeightKg,
                        set.RepsCompleted,
                        set.IsWarmup,
                        set.RestTimeSeconds));
                }

                groupUi.RefreshSummary();
                ExerciseGroups.Add(groupUi);
            }

            IsDetailSheetVisible = true;
        }
        catch (Exception ex)
        {
            ShowFailure(ex, "Workout_History_Error_Detail");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Closes the session detail.</summary>
    [RelayCommand]
    public void CloseSessionDetail()
    {
        IsDetailSheetVisible = false;
    }

    /// <summary>Saves an edited set.</summary>
    [RelayCommand]
    public async Task SaveSetEditAsync(EditableExerciseSetItem? item)
    {
        if (item is null || SelectedSessionDetail is null)
            return;

        item.SyncFromInputs();
        item.IsSaving = true;
        try
        {
            await _workoutArchiveUseCase.UpdateExerciseSetAsync(
                item.Id,
                item.WeightKg,
                item.RepsCompleted);

            item.IsSaved = true;

            // Refresh group summary
            var group = ExerciseGroups.FirstOrDefault(g => g.EditableSets.Contains(item));
            group?.RefreshSummary();

            // Recalculate detail volume
            var totalVol = ExerciseGroups.SelectMany(g => g.EditableSets)
                .Where(s => !s.IsWarmup)
                .Sum(s => s.WeightKg * s.RepsCompleted);
            DetailVolumeFormatted = FormatKilograms(totalVol);

            // Update in-memory session summary item in Sessions feed
            var match = Sessions.FirstOrDefault(s => s.Id == SelectedSessionDetail.Id);
            if (match is not null)
            {
                var diff = Math.Round(totalVol, 1) - match.TotalVolumeKg;
                var updatedVolume = Math.Max(0, Math.Round(LifetimeVolumeKg + diff, 0));
                LifetimeVolumeKg = updatedVolume;
                LifetimeVolumeFormatted = FormatKilograms(LifetimeVolumeKg);

                var updatedSummary = match with
                {
                    TotalVolumeKg = Math.Round(totalVol, 1)
                };
                var idx = Sessions.IndexOf(match);
                Sessions[idx] = updatedSummary;
            }

            ShowStatus(UiTextCatalog.Format(
                "Workout_History_SetUpdatedFormat",
                item.SetNumber,
                item.WeightKg,
                item.RepsCompleted));
        }
        catch (Exception ex)
        {
            ShowFailure(ex, "Workout_History_Error_UpdateSet");
        }
        finally
        {
            item.IsSaving = false;
        }
    }

    /// <summary>True when the history has sessions.</summary>
    public bool HasSessions => Sessions.Count > 0;
    /// <summary>True when a month filter is applied.</summary>
    public bool IsFilteredByMonth => SelectedMonth is not null && !SelectedMonth.IsAllMonths;
    /// <summary>True when the first load failed.</summary>
    public bool ShowInitialLoadError => IsError && Sessions.Count == 0;
    /// <summary>True when a page load failed.</summary>
    public bool ShowFooterLoadError => IsError && Sessions.Count > 0;

    /// <summary>Refreshes the error flags when the state changes.</summary>
    partial void OnIsErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInitialLoadError));
        OnPropertyChanged(nameof(ShowFooterLoadError));
    }

    /// <summary>Refreshes the empty state when sessions change.</summary>
    partial void OnHasNoSessionsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSessions));
    }

    /// <summary>Refreshes the filter flag when the month changes.</summary>
    partial void OnSelectedMonthChanged(MonthFilterItem? value)
    {
        OnPropertyChanged(nameof(IsFilteredByMonth));
    }

    /// <summary>Refreshes the edit state when the effort text changes.</summary>
    partial void OnEditingRpeTextChanged(string value)
    {
        RpeValidationError = string.Empty;
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            EditingRpe = null;
            return;
        }

        if (int.TryParse(trimmed, out var parsed) && parsed is >= 1 and <= 10)
            EditingRpe = parsed;
    }

    /// <summary>Retries the failed load.</summary>
    [RelayCommand]
    public Task RetryAsync()
    {
        return Sessions.Count == 0 ? LoadHistoryAsync() : LoadNextPageAsync();
    }

    /// <summary>Saves the session notes.</summary>
    [RelayCommand]
    public async Task SaveSessionNotesAsync()
    {
        if (SelectedSessionDetail is null || IsSavingNotes)
            return;

        var trimmedRpe = EditingRpeText?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(trimmedRpe))
        {
            if (!int.TryParse(trimmedRpe, out var parsedRpe) || parsedRpe is < 1 or > 10)
            {
                RpeValidationError = "Workout_History_RpeRangeError";
                return;
            }
            EditingRpe = parsedRpe;
        }
        else
        {
            EditingRpe = null;
        }

        RpeValidationError = string.Empty;
        IsSavingNotes = true;
        try
        {
            await _workoutArchiveUseCase.UpdateWorkoutSessionNotesAsync(
                SelectedSessionDetail.Id,
                EditingNotes.Trim(),
                EditingRpe);

            // Update in-memory session summary item in Sessions feed
            var match = Sessions.FirstOrDefault(s => s.Id == SelectedSessionDetail.Id);
            if (match is not null)
            {
                var updatedSummary = match with
                {
                    Notes = EditingNotes.Trim(),
                    RpeRating = EditingRpe
                };
                var idx = Sessions.IndexOf(match);
                Sessions[idx] = updatedSummary;
            }

            ShowStatus(UiTextCatalog.Get("Workout_History_NotesUpdatedMessage"));
        }
        catch (Exception ex)
        {
            ShowFailure(ex, "Workout_History_Error_Save");
        }
        finally
        {
            IsSavingNotes = false;
        }
    }

    /// <summary>Returns to the previous page.</summary>
    [RelayCommand]
    public async Task NavigateBackAsync()
    {
        if (_navigateBackAsync is not null)
            await _navigateBackAsync();
    }

    /// <summary>Reports a failure as a notice, and as the page's error state when the failure left nothing to show.</summary>
    private void ShowFailure(Exception exception, string fallbackKey, bool markLoadError = false)
    {
        var notice = ApiFailurePresenter.FromException(exception, fallbackKey);
        if (markLoadError)
        {
            IsError = true;
            ErrorMessage = notice.MessageText;
        }

        ShowStatus(notice.MessageText);
    }

    /// <summary>A weight in kilograms as text.</summary>
    private static string FormatKilograms(double kilograms) =>
        UiTextCatalog.Format(
            "Workout_History_ValueKgFormat",
            kilograms > KilogramRoundingThreshold
                ? DisplayFormat.Number(kilograms)
                : DisplayFormat.WholeNumber(kilograms));

    /// <summary>Shows a transient status line; a newer message supersedes the older one's timer.</summary>
    private void ShowStatus(string message)
    {
        StatusMessage = message;
        IsStatusVisible = true;
        var version = ++_statusVersion;
        _ = HideStatusAsync(version);
    }

    /// <summary>Hides the status line after its delay, unless a newer message has replaced it meanwhile.</summary>
    private async Task HideStatusAsync(int version)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(StatusMessageVisibilityMilliseconds));
        if (version != _statusVersion)
            return;

        await MainThread.InvokeOnMainThreadAsync(() => IsStatusVisible = false);
    }
}
