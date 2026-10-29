using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UseCases.Features.Cycle;
using MuscleCuties.UseCases.Features.Cycle.DailyLog;

namespace MuscleCuties.UI.ViewModels.Cycle;

public partial class CycleViewModel
{
    [ObservableProperty] private ObservableCollection<FlowOptionItem> _flowOptions = new();
    [ObservableProperty] private bool _isPeriodEnded;
    [ObservableProperty] private bool _isPeriodStarted;
    [ObservableProperty] private bool _isQuickLogLoading;
    [ObservableProperty] private bool _isQuickLogSheetVisible;
    [ObservableProperty] private string _quickLogDateText = string.Empty;
    private int _quickLogLoadVersion;
    [ObservableProperty] private string _quickLogStatusText = string.Empty;

    /// <summary>True when the last save was rejected because symptoms had no active cycle to attach to.</summary>
    [ObservableProperty] private bool _isQuickLogPeriodStartNeeded;
    [ObservableProperty] private FlowIntensity? _selectedFlowIntensity;
    [ObservableProperty] private ObservableCollection<SymptomOptionItem> _symptomOptions = new();

    public RelayCommand OpenQuickLogCommand { get; private set; } = null!;
    public RelayCommand CloseQuickLogSheetCommand { get; private set; } = null!;
    public RelayCommand TogglePeriodStartedCommand { get; private set; } = null!;
    public RelayCommand TogglePeriodEndedCommand { get; private set; } = null!;
    public RelayCommand StartPeriodFromQuickLogCommand { get; private set; } = null!;
    public RelayCommand<FlowOptionItem> SelectFlowCommand { get; private set; } = null!;
    public RelayCommand<SymptomOptionItem> ToggleSymptomCommand { get; private set; } = null!;
    public AsyncRelayCommand SaveQuickLogCommand { get; private set; } = null!;

    private void InitializeQuickLogCommands()
    {
        OpenQuickLogCommand = new RelayCommand(() => OpenQuickLog());
        CloseQuickLogSheetCommand = new RelayCommand(CloseQuickLogSheet);
        TogglePeriodStartedCommand = new RelayCommand(TogglePeriodStarted);
        TogglePeriodEndedCommand = new RelayCommand(TogglePeriodEnded);
        StartPeriodFromQuickLogCommand = new RelayCommand(StartPeriodFromQuickLog);
        SelectFlowCommand = new RelayCommand<FlowOptionItem>(SelectFlow);
        ToggleSymptomCommand = new RelayCommand<SymptomOptionItem>(ToggleSymptom);
        SaveQuickLogCommand = new AsyncRelayCommand(SaveQuickLogCoreAsync, () => !IsQuickLogLoading && !IsBusy);

        FlowOptions = BuildFlowOptions(null);
        SymptomOptions = BuildSymptomOptions(null);
    }

    public void OpenQuickLog(CycleDayItem? day = null)
    {
        var targetDate = day?.Date ?? SelectedDate;
        SelectedDate = targetDate;
        QuickLogDateText = targetDate.ToString("dddd", CultureInfo.CurrentCulture) + ", " +
                           targetDate.ToString("m", CultureInfo.CurrentCulture) +
                           (day?.HasRecordedObservation == true ? " · Recorded" :
                               day?.IsPredictedFuture == true ? " · Predicted" : string.Empty);
        QuickLogStatusText = string.Empty;
        IsQuickLogPeriodStartNeeded = false;

        // An estimated menstrual phase is not evidence that a period started.
        IsPeriodStarted = false;
        IsPeriodEnded = false;
        SelectedFlowIntensity = null;

        FlowOptions = BuildFlowOptions(SelectedFlowIntensity);
        SymptomOptions = BuildSymptomOptions(null);
        IsQuickLogSheetVisible = true;
        IsQuickLogLoading = true;
        SaveQuickLogCommand.NotifyCanExecuteChanged();

        var loadVersion = Interlocked.Increment(ref _quickLogLoadVersion);
        _ = LoadExistingQuickLogForDateAsync(targetDate, loadVersion);
    }

    private async Task LoadExistingQuickLogForDateAsync(DateTime targetDate, int loadVersion)
    {
        try
        {
            var saved = await _dailyLog.LoadAsync(targetDate);

            if (loadVersion != Volatile.Read(ref _quickLogLoadVersion) ||
                !IsQuickLogSheetVisible || SelectedDate.Date != targetDate.Date)
                return;

            IsPeriodStarted = saved?.IsPeriodStarted == true;
            IsPeriodEnded = saved?.IsPeriodEnded == true;
            SelectedFlowIntensity = saved?.FlowIntensity;

            FlowOptions = BuildFlowOptions(SelectedFlowIntensity);
            SymptomOptions = BuildSymptomOptions(saved?.Symptoms);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Could not load symptoms for date ({ex.GetType().Name})");
            if (loadVersion == Volatile.Read(ref _quickLogLoadVersion))
                QuickLogStatusText = "Could not load the saved entry. Close and try again.";
        }
        finally
        {
            if (loadVersion == Volatile.Read(ref _quickLogLoadVersion))
            {
                IsQuickLogLoading = false;
                SaveQuickLogCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void CloseQuickLogSheet()
    {
        IsQuickLogSheetVisible = false;
        IsQuickLogPeriodStartNeeded = false;
        Interlocked.Increment(ref _quickLogLoadVersion);
        IsQuickLogLoading = false;
    }

    /// <summary>
    /// Recovery action for the "symptoms without an active cycle" rejection: turn the visible period-start
    /// toggle on for the selected day, so the next save creates the cycle the symptoms attach to.
    /// </summary>
    private void StartPeriodFromQuickLog()
    {
        if (IsQuickLogLoading) return;

        if (!IsPeriodStarted)
            TogglePeriodStarted();

        IsQuickLogPeriodStartNeeded = false;
        QuickLogStatusText = string.Empty;
    }

    private void TogglePeriodStarted()
    {
        if (IsQuickLogLoading) return;
        IsPeriodStarted = !IsPeriodStarted;
        if (IsPeriodStarted)
        {
            IsPeriodEnded = false;
            if (SelectedFlowIntensity is null or FlowIntensity.None)
                SelectFlow(FlowOptions.FirstOrDefault(f => f.Intensity == FlowIntensity.Medium));
        }
    }

    private void TogglePeriodEnded()
    {
        if (IsQuickLogLoading) return;
        IsPeriodEnded = !IsPeriodEnded;
        if (IsPeriodEnded)
        {
            IsPeriodStarted = false;
            SelectedFlowIntensity = FlowIntensity.None;
            FlowOptions = BuildFlowOptions(null);
        }
    }

    private void SelectFlow(FlowOptionItem? option)
    {
        if (option is null || IsQuickLogLoading) return;

        var wasSelected = FlowOptions.Any(flow => flow.Intensity == option.Intensity && flow.IsSelected);

        if (!wasSelected)
        {
            SelectedFlowIntensity = option.Intensity;
            if (option.Intensity != FlowIntensity.None && !IsPeriodEnded) IsPeriodStarted = true;
        }
        else
        {
            SelectedFlowIntensity = null;
        }

        FlowOptions = BuildFlowOptions(SelectedFlowIntensity);
    }

    private void ToggleSymptom(SymptomOptionItem? symptom)
    {
        if (symptom is null || IsQuickLogLoading) return;
        var selected = SymptomOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Type)
            .ToHashSet();

        if (!selected.Add(symptom.Type))
            selected.Remove(symptom.Type);

        SymptomOptions = BuildSymptomOptions(selected.ToList());
    }

    private async Task SaveQuickLogCoreAsync()
    {
        if (IsBusy || IsQuickLogLoading) return;

        if (!IsPeriodStarted && !IsPeriodEnded && SelectedFlowIntensity is null &&
            SymptomOptions.All(symptom => !symptom.IsSelected))
        {
            QuickLogStatusText = "Choose a period update, flow, or symptom before saving.";
            return;
        }

        IsBusy = true;
        try
        {
            var selectedSymptoms = SymptomOptions
                .Where(s => s.IsSelected)
                .Select(s => s.Type)
                .ToList();

            var flow = FlowOptions.FirstOrDefault(f => f.IsSelected)?.Intensity ?? SelectedFlowIntensity;

            if (!await _dailyLog.SaveAsync(new CycleDailyLogRequest(
                    SelectedDate, IsPeriodStarted, IsPeriodEnded, flow,
                    selectedSymptoms)))
            {
                QuickLogStatusText = "Active cycle account unavailable.";
                return;
            }

            IsQuickLogSheetVisible = false;
            await RefreshCycleDataAsync();
            NotifyCycleMutated();
        }
        catch (CycleSymptomsNeedActiveCycleIssue)
        {
            IsQuickLogPeriodStartNeeded = true;
            QuickLogStatusText = "Record a period start before adding symptoms without a known cycle phase.";
        }
        catch (CyclePhaseOrderIssue ex)
        {
            QuickLogStatusText = $"This phase would skip a step. Log {ex.SuggestedPhase} first.";
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Cycle] Quick log save failed ({ex.GetType().Name}).");
            QuickLogStatusText = "Could not save log. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsQuickLogLoadingChanged(bool value)
    {
        SaveQuickLogCommand?.NotifyCanExecuteChanged();
    }

    private static ObservableCollection<FlowOptionItem> BuildFlowOptions(FlowIntensity? selected)
    {
        return new ObservableCollection<FlowOptionItem>
        {
            new()
            {
                Intensity = FlowIntensity.Spotting,
                Name = "Spotting",
                Description = "Very light trace",
                IsSelected = selected == FlowIntensity.Spotting
            },
            new()
            {
                Intensity = FlowIntensity.Light,
                Name = "Light",
                Description = "Light bleeding",
                IsSelected = selected == FlowIntensity.Light
            },
            new()
            {
                Intensity = FlowIntensity.Medium,
                Name = "Medium",
                Description = "Standard flow",
                IsSelected = selected == FlowIntensity.Medium
            },
            new()
            {
                Intensity = FlowIntensity.Heavy,
                Name = "Heavy",
                Description = "Heavy flow",
                IsSelected = selected == FlowIntensity.Heavy
            }
        };
    }

    private static ObservableCollection<SymptomOptionItem> BuildSymptomOptions(
        IReadOnlyList<SymptomType>? selectedSymptoms)
    {
        var set = selectedSymptoms is not null
            ? new HashSet<SymptomType>(selectedSymptoms)
            : new HashSet<SymptomType>();

        return new ObservableCollection<SymptomOptionItem>
        {
            new()
            {
                Type = SymptomType.Cramps,
                Name = "Cramps",
                IconData = "M22 12h-4l-3 9L9 3l-3 9H2",
                IsSelected = set.Contains(SymptomType.Cramps)
            },
            new()
            {
                Type = SymptomType.Bloating,
                Name = "Bloating",
                IconData = "M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z",
                IsSelected = set.Contains(SymptomType.Bloating)
            },
            new()
            {
                Type = SymptomType.Fatigue,
                Name = "Fatigue",
                IconData = "M13 2L3 14h9l-1 8 10-12h-9l1-8z",
                IsSelected = set.Contains(SymptomType.Fatigue)
            },
            new()
            {
                Type = SymptomType.Headache,
                Name = "Headache",
                IconData =
                    "M12 3a9 9 0 0 0-9 9c0 3.1 1.6 5.8 4 7.4V21h10v-1.6c2.4-1.6 4-4.3 4-7.4a9 9 0 0 0-9-9zm-3 8h6M12 8v6",
                IsSelected = set.Contains(SymptomType.Headache)
            },
            new()
            {
                Type = SymptomType.MoodSwings,
                Name = "Mood",
                IconData =
                    "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z",
                IsSelected = set.Contains(SymptomType.MoodSwings)
            },
            new()
            {
                Type = SymptomType.Spotting,
                Name = "Spotting",
                IconData = "M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0zM12 11a2 2 0 1 0 0 4 2 2 0 0 0 0-4z",
                IsSelected = set.Contains(SymptomType.Spotting)
            },
            new()
            {
                Type = SymptomType.BreastTenderness,
                Name = "Tender Breasts",
                IconData =
                    "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z",
                IsSelected = set.Contains(SymptomType.BreastTenderness)
            },
            new()
            {
                Type = SymptomType.LowerBackPain,
                Name = "Back Pain",
                IconData = "M12 2v20M8 5h8M7 9h10M6 13h12M7 17h10M8 21h8",
                IsSelected = set.Contains(SymptomType.LowerBackPain)
            },
            new()
            {
                Type = SymptomType.Acne,
                Name = "Acne",
                IconData = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zm0-6v3m0 14v3m-9-9h3m14 0h3",
                IsSelected = set.Contains(SymptomType.Acne)
            },
            new()
            {
                Type = SymptomType.Nausea,
                Name = "Nausea",
                IconData =
                    "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm-3 7a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3zm6 0a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3zm-6.5 6c1 1.5 2.5 2 3.5 2s2.5-.5 3.5-2",
                IsSelected = set.Contains(SymptomType.Nausea)
            },
            new()
            {
                Type = SymptomType.Cravings,
                Name = "Cravings",
                IconData = "M18 8h1a4 4 0 0 1 0 8h-1M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8zm4-6v3m4-3v3m4-3v3",
                IsSelected = set.Contains(SymptomType.Cravings)
            },
            new()
            {
                Type = SymptomType.Other,
                Name = "Other",
                IconData = "M12 5v14M5 12h14",
                IsSelected = set.Contains(SymptomType.Other)
            }
        };
    }
}
