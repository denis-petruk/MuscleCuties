using Microsoft.Maui.Controls;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Dashboard;

namespace MuscleCuties.UI.Pages.Dashboard;

public partial class DailyCheckInPage : ContentPage
{
    private readonly DailyCheckInViewModel _viewModel;

    public DailyCheckInPage(DailyCheckInViewModel vm)
    {
        _viewModel = this.InitializeAndBind(InitializeComponent, vm);
    }

    private Button[] EnergyChips => [EnergyChip1, EnergyChip2, EnergyChip3, EnergyChip4, EnergyChip5];
    private Button[] PainChips => [PainChip0, PainChip1, PainChip2, PainChip3];

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged -= OnRequestedThemeChanged;
            app.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        this.BeginPageLoad(LoadPageAsync);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        if (Application.Current is { } app)
            app.RequestedThemeChanged -= OnRequestedThemeChanged;
        base.OnNavigatedFrom(args);
    }

    protected override void OnDisappearing()
    {
        if (Application.Current is { } app)
            app.RequestedThemeChanged -= OnRequestedThemeChanged;
        base.OnDisappearing();
    }

    private async Task LoadPageAsync()
    {
        await _viewModel.CheckIfAlreadyCompletedAsync();
        UpdateEnergyChips();
        UpdatePainChips();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        UpdateEnergyChips();
        UpdatePainChips();
    }

    private void OnEnergyClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: { } parameter } && int.TryParse(parameter.ToString(), out var level))
        {
            _viewModel.EnergyLevel = level;
            UpdateEnergyChips();
        }
    }

    private void OnPainClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: { } parameter } && int.TryParse(parameter.ToString(), out var level))
        {
            _viewModel.PainLevel = level;
            UpdatePainChips();
        }
    }

    private void UpdateEnergyChips()
    {
        UpdateChipGroup(EnergyChips, (_viewModel.EnergyLevel ?? 0) - 1,
            (i, sel) => UiTextCatalog.Translate($"Energy {i + 1} of 5") +
                        (sel ? ", " + UiTextCatalog.Translate("Selected") : string.Empty));
    }

    private void UpdatePainChips()
    {
        UpdateChipGroup(PainChips, _viewModel.PainLevel,
            (i, sel) => UiTextCatalog.Translate(i switch
            {
                0 => "Pain: None",
                1 => "Pain: Mild",
                2 => "Pain: Moderate",
                _ => "Pain: Severe"
            }) + (sel ? ", " + UiTextCatalog.Translate("Selected") : string.Empty));
    }

    private static void UpdateChipGroup(Button[] chips, int selectedIndex, Func<int, bool, string> buildDescription)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var resources = Application.Current!.Resources;
        var selectedBg = isDark ? (Color)resources["ChoiceSelectedSurfaceDark"] : (Color)resources["ChoiceSelectedSurfaceLight"];
        var selectedBorder = isDark ? (Color)resources["ChoiceSelectedStrokeDark"] : (Color)resources["ChoiceSelectedStrokeLight"];
        var unselectedBg = isDark ? (Color)resources["Gray900"] : (Color)resources["Gray100"];
        var unselectedBorder = isDark ? (Color)resources["Gray700"] : (Color)resources["Gray300"];
        var selectedText = selectedBorder;
        var unselectedText = isDark ? (Color)resources["TextPrimaryDark"] : (Color)resources["TextPrimary"];

        for (var i = 0; i < chips.Length; i++)
        {
            var selected = i == selectedIndex;
            chips[i].BackgroundColor = selected ? selectedBg : unselectedBg;
            chips[i].BorderColor = selected ? selectedBorder : unselectedBorder;
            chips[i].TextColor = selected ? selectedText : unselectedText;
            SemanticProperties.SetDescription(chips[i], buildDescription(i, selected));
        }
    }
}
