using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UseCases.Features.Workout.Archive;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class WorkoutHistoryViewModel : ObservableObject
{
    private const int PageSize = 25;
    private readonly Func<Task>? _navigateBackAsync;
    private readonly IWorkoutArchiveUseCase _workoutArchiveUseCase;

    [ObservableProperty] private string _currentFilterTitle = "All Months";

    private int _currentOffset;

    [ObservableProperty] private string _detailDurationFormatted = string.Empty;

    [ObservableProperty] private string _detailKindLabel = string.Empty;

    [ObservableProperty] private string _detailStartedFormatted = string.Empty;

    [ObservableProperty] private string _detailVolumeFormatted = string.Empty;

    [ObservableProperty] private string _editingNotes = string.Empty;

    [ObservableProperty] private int? _editingRpe;

    [ObservableProperty] private string _errorMessage = string.Empty;

    private int _filterRequestId;

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

    [ObservableProperty] private string _lifetimeVolumeFormatted = "0 kg";

    [ObservableProperty] private double _lifetimeVolumeKg;

    [ObservableProperty] private MonthFilterItem? _selectedMonth;

    [ObservableProperty] private WorkoutArchiveDetailDto? _selectedSessionDetail;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private int _totalSessionsCount;

    public WorkoutHistoryViewModel(
        IWorkoutArchiveUseCase workoutArchiveUseCase,
        Func<Task>? navigateBackAsync = null)
    {
        _workoutArchiveUseCase = workoutArchiveUseCase;
        _navigateBackAsync = navigateBackAsync;
    }

    public ObservableCollection<WorkoutArchiveSummaryDto> Sessions { get; } = [];

    public string DetailRpeFormatted => EditingRpe is { } rpe ? $"{rpe}/10" : "Not recorded";

    public ObservableCollection<MonthFilterItem> AvailableMonths { get; } = [];

    public ObservableCollection<ExerciseSetsUiGroup> ExerciseGroups { get; } = [];

    partial void OnEditingRpeChanged(int? value)
    {
        OnPropertyChanged(nameof(DetailRpeFormatted));
    }

    public async Task InitializeAsync()
    {
        await LoadHistoryAsync();
    }

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
                var allItem = new MonthFilterItem(0, 0, "All Months", true) { IsSelected = true };
                AvailableMonths.Add(allItem);
                SelectedMonth = allItem;

                var distinctMonths = (await _workoutArchiveUseCase.GetAvailableHistoryMonthsAsync()).ToList();
                if (requestId != _filterRequestId) return;

                var now = DateTime.UtcNow;
                if (!distinctMonths.Any(p => p.Year == now.Year && p.Month == now.Month))
                    distinctMonths.Insert(0, new HistoryMonthOptionDto(now.Year, now.Month));

                foreach (var option in distinctMonths)
                {
                    var dt = new DateTime(option.Year, option.Month, 1);
                    AvailableMonths.Add(new MonthFilterItem(option.Year, option.Month,
                        dt.ToString("MMM yyyy", CultureInfo.CurrentCulture)));
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
                IsError = true;
                ErrorMessage = ex.Message;
                ShowStatus($"Error loading workouts: {ex.Message}");
            }
        }
        finally
        {
            if (requestId == _filterRequestId) IsLoading = false;
        }
    }

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

    [RelayCommand]
    public async Task ShowAllMonthsAsync()
    {
        var all = AvailableMonths.FirstOrDefault(m => m.IsAllMonths);
        if (all is not null)
            await SelectMonthAsync(all);
    }

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
                IsError = true;
                ErrorMessage = ex.Message;
                ShowStatus($"Error filtering workouts: {ex.Message}");
            }
        }
        finally
        {
            if (requestId == _filterRequestId) IsLoading = false;
        }
    }

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
                IsError = true;
                ErrorMessage = ex.Message;
                ShowStatus($"Error loading more workouts: {ex.Message}");
            }
        }
        finally
        {
            if (requestId == _filterRequestId) IsLoadingMore = false;
        }
    }

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
        LifetimeVolumeFormatted = LifetimeVolumeKg > 1000
            ? $"{LifetimeVolumeKg:N0} kg"
            : $"{LifetimeVolumeKg:F0} kg";

        HasNoSessions = Sessions.Count == 0;
    }

    [RelayCommand]
    public async Task OpenSessionDetailAsync(WorkoutArchiveSummaryDto summary)
    {
        IsLoading = true;
        try
        {
            var detail = await _workoutArchiveUseCase.GetWorkoutSessionDetailAsync(summary.Id);
            SelectedSessionDetail = detail;

            DetailDurationFormatted = detail.TotalDurationSeconds > 0
                ? $"{Math.Round(detail.TotalDurationSeconds / 60.0):N0} min"
                : "Not recorded";
            DetailStartedFormatted = detail.TimePrecision == "legacyUnknown"
                ? detail.StartedAt.TimeOfDay == TimeSpan.Zero
                    ? detail.StartedAt.ToString("D", CultureInfo.CurrentCulture)
                    : detail.StartedAt.ToString("f", CultureInfo.CurrentCulture) + " · time zone unknown"
                : detail.StartedAt.ToString("f", CultureInfo.CurrentCulture);
            DetailVolumeFormatted = detail.DetailKind == "aggregateOnly" && detail.TotalVolumeKg <= 0
                ? "Not recorded"
                : $"{detail.TotalVolumeKg:N0} kg";
            DetailKindLabel = detail.DetailKind == "aggregateOnly"
                ? "Historical aggregate: individual sets were not recorded."
                : detail.DetailKind == "noSetDetails"
                    ? "Set details were not recorded."
                    : string.Empty;
            EditingNotes = detail.Notes ?? string.Empty;
            EditingRpe = detail.RpeRating;

            ExerciseGroups.Clear();
            var grouped = detail.Sets
                .GroupBy(s => s.ExerciseName, StringComparer.OrdinalIgnoreCase);

            foreach (var grp in grouped)
            {
                var groupUi = new ExerciseSetsUiGroup(grp.Key);
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
            ShowStatus($"Error opening detail: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void CloseSessionDetail()
    {
        IsDetailSheetVisible = false;
    }

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
            DetailVolumeFormatted = $"{totalVol:N0} kg";

            // Update in-memory session summary item in Sessions feed
            var match = Sessions.FirstOrDefault(s => s.Id == SelectedSessionDetail.Id);
            if (match is not null)
            {
                var diff = Math.Round(totalVol, 1) - match.TotalVolumeKg;
                var updatedVolume = Math.Max(0, Math.Round(LifetimeVolumeKg + diff, 0));
                LifetimeVolumeKg = updatedVolume;
                LifetimeVolumeFormatted = LifetimeVolumeKg > 1000
                    ? $"{LifetimeVolumeKg:N0} kg"
                    : $"{LifetimeVolumeKg:F0} kg";

                var updatedSummary = match with
                {
                    TotalVolumeKg = Math.Round(totalVol, 1)
                };
                var idx = Sessions.IndexOf(match);
                Sessions[idx] = updatedSummary;
            }

            ShowStatus($"Set #{item.SetNumber} updated ({item.WeightKg:F1}kg × {item.RepsCompleted} reps)");
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to update set: {ex.Message}");
        }
        finally
        {
            item.IsSaving = false;
        }
    }

    [RelayCommand]
    public async Task SaveSessionNotesAsync()
    {
        if (SelectedSessionDetail is null)
            return;

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

            ShowStatus("Notes and RPE updated.");
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to save: {ex.Message}");
        }
        finally
        {
            IsSavingNotes = false;
        }
    }

    [RelayCommand]
    public async Task NavigateBackAsync()
    {
        if (_navigateBackAsync is not null)
            await _navigateBackAsync();
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        IsStatusVisible = true;
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            IsStatusVisible = false;
        });
    }
}
