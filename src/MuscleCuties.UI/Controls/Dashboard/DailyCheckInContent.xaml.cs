using System.ComponentModel;
using Microsoft.Maui.Controls;
using MuscleCuties.UI.ViewModels.Dashboard;

namespace MuscleCuties.UI.Controls.Dashboard;

/// <summary>
/// Chip selection for the daily check-in. Selection is a visual state of the permitted button
/// styles; the accessible name and hint stay as authored in the localized markup.
/// </summary>
public partial class DailyCheckInContent : ContentView
{
    private const string SelectedVisualState = "Selected";
    private const string UnselectedVisualState = "Unselected";

    private DailyCheckInViewModel? _subscribedViewModel;
    private bool _isThemeSubscribed;

    public DailyCheckInContent()
    {
        InitializeComponent();
    }

    private Button[] SleepQualityChips =>
        [SleepQualityChip1, SleepQualityChip2, SleepQualityChip3, SleepQualityChip4, SleepQualityChip5];

    private Button[] EnergyChips => [EnergyChip1, EnergyChip2, EnergyChip3, EnergyChip4, EnergyChip5];
    private Button[] PainChips => [PainChip0, PainChip1, PainChip2, PainChip3];
    private Button[] SorenessChips => [SorenessChip1, SorenessChip2, SorenessChip3, SorenessChip4, SorenessChip5];
    private Button[] BloatingChips => [BloatingNoChip, BloatingYesChip];

    protected override void OnBindingContextChanged()
    {
        DetachViewModel();
        base.OnBindingContextChanged();
        AttachViewModel();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
            DetachViewModel();
        else
            AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (BindingContext is not DailyCheckInViewModel vm || ReferenceEquals(_subscribedViewModel, vm))
            return;

        DetachViewModel();
        _subscribedViewModel = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        SubscribeThemeChange();
        UpdateChipGroups();
    }

    private void DetachViewModel()
    {
        if (_subscribedViewModel is { } vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribedViewModel = null;
        UnsubscribeThemeChange();
    }

    private void SubscribeThemeChange()
    {
        if (_isThemeSubscribed || Application.Current is not { } app)
            return;

        app.RequestedThemeChanged += OnRequestedThemeChanged;
        _isThemeSubscribed = true;
    }

    private void UnsubscribeThemeChange()
    {
        if (!_isThemeSubscribed || Application.Current is not { } app)
            return;

        app.RequestedThemeChanged -= OnRequestedThemeChanged;
        _isThemeSubscribed = false;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => UpdateChipGroups();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DailyCheckInViewModel.PlanningSleepQuality)
            or nameof(DailyCheckInViewModel.EnergyLevel)
            or nameof(DailyCheckInViewModel.PainLevel)
            or nameof(DailyCheckInViewModel.PlanningSoreness)
            or nameof(DailyCheckInViewModel.HasBloating))
            UpdateChipGroups();
    }

    private void OnSleepQualityClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.PlanningSleepQuality = level;
            UpdateChipGroups();
        }
    }

    private void OnEnergyClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.EnergyLevel = level;
            UpdateChipGroups();
        }
    }

    private void OnPainClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.PainLevel = level;
            UpdateChipGroups();
        }
    }

    private void OnSorenessClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.PlanningSoreness = level;
            UpdateChipGroups();
        }
    }

    private void OnBloatingClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.HasBloating = level == 1;
            UpdateChipGroups();
        }
    }

    private static bool TryReadLevel(object? sender, out int level)
    {
        level = 0;
        return sender is Button { CommandParameter: { } parameter } &&
               int.TryParse(parameter.ToString(), out level);
    }

    private void UpdateChipGroups()
    {
        if (BindingContext is not DailyCheckInViewModel vm)
            return;

        UpdateChipGroup(SleepQualityChips, (vm.PlanningSleepQuality ?? 0) - 1);
        UpdateChipGroup(EnergyChips, (vm.EnergyLevel ?? 0) - 1);
        UpdateChipGroup(PainChips, vm.PainLevel ?? -1);
        UpdateChipGroup(SorenessChips, (vm.PlanningSoreness ?? 0) - 1);
        UpdateChipGroup(BloatingChips, vm.HasBloating switch { false => 0, true => 1, null => -1 });
    }

    private static void UpdateChipGroup(Button[] chips, int selectedIndex)
    {
        for (var i = 0; i < chips.Length; i++)
        {
            VisualStateManager.GoToState(
                chips[i],
                i == selectedIndex ? SelectedVisualState : UnselectedVisualState);
        }
    }
}
