using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UI.Models.UI.Cycle;

namespace MuscleCuties.UI.ViewModels.Cycle;

public partial class CyclePhaseDetailViewModel : ObservableObject
{
    private readonly Func<Task> _navigateBackAsync;

    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Unknown;
    [ObservableProperty] private string _headline = "Phase not yet known";
    [ObservableProperty] private string _hook = "Log a period start or phase to see guidance here.";
    [ObservableProperty] private bool _isWatchExpanded;
    [ObservableProperty] private string _nutritionLead = string.Empty;
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _nutritionPoints = new();
    [ObservableProperty] private double _nutritionSignal;
    [ObservableProperty] private string _nutritionSignalText = "Unknown";
    [ObservableProperty] private string _phaseIconGlyph = "QuestionCircle24";
    [ObservableProperty] private string _phaseImageSource = string.Empty;
    [ObservableProperty] private bool _phaseImageUsesAnimation;
    [ObservableProperty] private string _phaseName = "Cycle phase unknown";
    [ObservableProperty] private string _phaseWindow = "No phase estimate";
    [ObservableProperty] private string _quickRule = string.Empty;
    [ObservableProperty] private double _recoverySignal;
    [ObservableProperty] private string _recoverySignalText = "Unknown";
    [ObservableProperty] private double _trainingSignal;
    [ObservableProperty] private string _trainingSignalText = "Unknown";
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _watchPoints = new();
    [ObservableProperty] private string _whyLead = string.Empty;
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _whyPoints = new();
    [ObservableProperty] private string _workoutLead = string.Empty;
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _workoutPoints = new();

    public CyclePhaseDetailViewModel(Func<Task> navigateBackAsync)
    {
        _navigateBackAsync = navigateBackAsync;
        BackCommand = new AsyncRelayCommand(_navigateBackAsync);
        ToggleWatchCommand = new RelayCommand(() => IsWatchExpanded = !IsWatchExpanded);
        Load(CyclePhase.Unknown);
    }

    public AsyncRelayCommand BackCommand { get; }
    public RelayCommand ToggleWatchCommand { get; }

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in PhaseDetail XAML / view model contract.")]
    public string TrainingIconGlyph => "Dumbbell24";

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in PhaseDetail XAML / view model contract.")]
    public string NutritionIconGlyph => "Food24";

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in PhaseDetail XAML / view model contract.")]
    public string RecoveryIconGlyph => "WeatherMoon24";

    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Bound in PhaseDetail XAML / view model contract.")]
    public string WatchIconGlyph => "AlertUrgent24";

    public void Load(CyclePhase phase)
    {
        var detail = CyclePhaseDetailCatalog.BuildDetail(phase);
        CurrentPhase = phase;
        PhaseName = detail.PhaseName;
        PhaseWindow = detail.PhaseWindow;
        Headline = detail.Headline;
        Hook = detail.Hook;
        WhyLead = detail.WhyLead;
        NutritionLead = detail.NutritionLead;
        WorkoutLead = detail.WorkoutLead;
        QuickRule = detail.QuickRule;
        PhaseIconGlyph = CyclePhaseDetailCatalog.GetPhaseIconGlyph(phase);
        PhaseImageSource = CyclePhaseDetailCatalog.GetPhaseVisualSource(phase);
        PhaseImageUsesAnimation = phase is CyclePhase.Menstrual or CyclePhase.Follicular or
            CyclePhase.Ovulatory or CyclePhase.Luteal;
        IsWatchExpanded = false;
        ApplySignalModel(phase);
        WhyPoints = BuildPointCollection(detail.WhyPoints);
        NutritionPoints = BuildPointCollection(detail.NutritionPoints);
        WorkoutPoints = BuildPointCollection(detail.WorkoutPoints);
        WatchPoints = BuildPointCollection(detail.WatchPoints);
    }

    private static ObservableCollection<CyclePhaseDetailPoint> BuildPointCollection(
        IReadOnlyList<CyclePhaseDetailPoint> points)
    {
        return new ObservableCollection<CyclePhaseDetailPoint>(points.Select(point => new CyclePhaseDetailPoint
        {
            Title = point.Title,
            Text = point.Text,
            IconGlyph = point.IconGlyph
        }));
    }

    private void ApplySignalModel(CyclePhase phase)
    {
        var signal = CyclePhaseDetailCatalog.GetPhaseSignals(phase);

        TrainingSignal = signal.Training;
        NutritionSignal = signal.Nutrition;
        RecoverySignal = signal.Recovery;
        TrainingSignalText = signal.TrainingText;
        NutritionSignalText = signal.NutritionText;
        RecoverySignalText = signal.RecoveryText;
    }
}
