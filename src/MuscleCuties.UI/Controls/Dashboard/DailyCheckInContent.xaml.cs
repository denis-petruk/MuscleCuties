using System.ComponentModel;
using Microsoft.Maui.Controls;
using MuscleCuties.UI.Controls.Shared;
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

    private readonly AppThemeSubscription _themeSubscription;

    /// <summary>Binds the check-in content to its view model and theme.</summary>
    public DailyCheckInContent()
    {
        InitializeComponent();
        _themeSubscription = new AppThemeSubscription(UpdateChipGroups);
        _ = ViewModelSubscriptionBinding.BindToControl<DailyCheckInViewModel>(this,
            OnViewModelPropertyChanged,
            onAttached: () =>
            {
                _themeSubscription.Attach();
                UpdateChipGroups();
            },
            onDetached: _themeSubscription.Detach);
    }

    /// <summary>The sleep-quality chips in level order, so a level can index its chip.</summary>
    private Button[] SleepQualityChips =>
        [SleepQualityChip1, SleepQualityChip2, SleepQualityChip3, SleepQualityChip4, SleepQualityChip5];

    /// <summary>The energy chips in level order.</summary>
    private Button[] EnergyChips => [EnergyChip1, EnergyChip2, EnergyChip3, EnergyChip4, EnergyChip5];
    /// <summary>The fatigue chips in level order.</summary>
    private Button[] FatigueChips => [FatigueChip1, FatigueChip2, FatigueChip3, FatigueChip4, FatigueChip5];
    /// <summary>The pain chips, which start at no pain so the array index is the level itself.</summary>
    private Button[] PainChips => [PainChip0, PainChip1, PainChip2, PainChip3];
    /// <summary>The soreness chips in level order.</summary>
    private Button[] SorenessChips => [SorenessChip1, SorenessChip2, SorenessChip3, SorenessChip4, SorenessChip5];
    /// <summary>The bloating chips, no first and yes second.</summary>
    private Button[] BloatingChips => [BloatingNoChip, BloatingYesChip];

    /// <summary>Repaints the chips only for the answered values, so unrelated property changes do not rebuild the whole</summary>
    /// form.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DailyCheckInViewModel.PlanningSleepQuality)
            or nameof(DailyCheckInViewModel.EnergyLevel)
            or nameof(DailyCheckInViewModel.PlanningFatigue)
            or nameof(DailyCheckInViewModel.PainLevel)
            or nameof(DailyCheckInViewModel.PlanningSoreness)
            or nameof(DailyCheckInViewModel.HasBloating))
            UpdateChipGroups();
    }

    /// <summary>Records the tapped sleep-quality level and repaints the group.</summary>
    private void OnSleepQualityClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.PlanningSleepQuality = level;
            UpdateChipGroups();
        }
    }

    /// <summary>Records the tapped energy level.</summary>
    private void OnEnergyClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.EnergyLevel = level;
            UpdateChipGroups();
        }
    }

    /// <summary>Records the tapped fatigue level.</summary>
    private void OnFatigueClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.PlanningFatigue = level;
            UpdateChipGroups();
        }
    }

    /// <summary>Records the tapped pain level, where the first chip means no pain.</summary>
    private void OnPainClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.PainLevel = level;
            UpdateChipGroups();
        }
    }

    /// <summary>Records the tapped soreness level.</summary>
    private void OnSorenessClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.PlanningSoreness = level;
            UpdateChipGroups();
        }
    }

    /// <summary>Records the tapped bloating answer, which is a yes or no rather than a level.</summary>
    private void OnBloatingClicked(object? sender, EventArgs e)
    {
        if (TryReadLevel(sender, out var level) && BindingContext is DailyCheckInViewModel vm)
        {
            vm.HasBloating = level == 1;
            UpdateChipGroups();
        }
    }

    /// <summary>The level a chip carries in its command parameter, so the handler never has to know which chip it was.</summary>
    private static bool TryReadLevel(object? sender, out int level)
    {
        level = 0;
        return sender is Button { CommandParameter: { } parameter } &&
               int.TryParse(parameter.ToString(), out level);
    }

    /// <summary>Repaints every group from the view model, which is what keeps the chips in step with an answer restored from</summary>
    /// a draft.</summary>
    private void UpdateChipGroups()
    {
        if (BindingContext is not DailyCheckInViewModel vm)
            return;

        UpdateChipGroup(SleepQualityChips, (vm.PlanningSleepQuality ?? 0) - 1);
        UpdateChipGroup(EnergyChips, (vm.EnergyLevel ?? 0) - 1);
        UpdateChipGroup(FatigueChips, (vm.PlanningFatigue ?? 0) - 1);
        UpdateChipGroup(PainChips, vm.PainLevel ?? -1);
        UpdateChipGroup(SorenessChips, (vm.PlanningSoreness ?? 0) - 1);
        UpdateChipGroup(BloatingChips, vm.HasBloating switch { false => 0, true => 1, null => -1 });
    }

    /// <summary>Lights the chip at an index and clears the rest, with a negative index meaning nothing is chosen.</summary>
    private static void UpdateChipGroup(Button[] chips, int selectedIndex)
    {
        var selectedHint = Services.Localization.UiTextCatalog.Get("Common_Selected");
        var unselectedHint = Services.Localization.UiTextCatalog.Get("Common_NotSelected");
        for (var i = 0; i < chips.Length; i++)
        {
            var isSelected = i == selectedIndex;
            VisualStateManager.GoToState(
                chips[i],
                isSelected ? SelectedVisualState : UnselectedVisualState);
            SemanticProperties.SetHint(chips[i], isSelected ? selectedHint : unselectedHint);
        }
    }
}
