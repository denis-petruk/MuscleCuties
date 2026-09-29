using MuscleCuties.Core.ViewModels.Dashboard;

namespace MuscleCuties.App.Pages.Dashboard;

public partial class DailyCheckInPage : ContentPage
{
    private readonly DailyCheckInViewModel _viewModel;

    private Button[] EnergyChips => [EnergyChip1, EnergyChip2, EnergyChip3, EnergyChip4, EnergyChip5];
    private Button[] PainChips => [PainChip0, PainChip1, PainChip2, PainChip3];

    public DailyCheckInPage(DailyCheckInViewModel vm)
    {
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = vm;
        BindingContext = vm;
    }

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
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var resources = Application.Current!.Resources;
        var selectedBg = (Color)resources["PrimaryDark"];
        var unselectedBg = isDark ? (Color)resources["CardSurfaceDark"] : (Color)resources["CardSurface"];
        var unselectedBorder = isDark ? (Color)resources["InputStrokeDark"] : (Color)resources["InputStroke"];
        var selectedText = (Color)resources["White"];
        var unselectedText = isDark ? (Color)resources["TextPrimaryDark"] : (Color)resources["TextPrimary"];

        var chips = EnergyChips;
        for (var i = 0; i < chips.Length; i++)
        {
            var selected = (i + 1) == _viewModel.EnergyLevel;
            chips[i].BackgroundColor = selected ? selectedBg : unselectedBg;
            chips[i].BorderColor = selected ? selectedBg : unselectedBorder;
            chips[i].TextColor = selected ? selectedText : unselectedText;
            SemanticProperties.SetDescription(chips[i], $"Energy {i + 1} of 5{(selected ? ", selected" : "")}");
        }
    }

    private void UpdatePainChips()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var resources = Application.Current!.Resources;
        var selectedBg = (Color)resources["PrimaryDark"];
        var unselectedBg = isDark ? (Color)resources["CardSurfaceDark"] : (Color)resources["CardSurface"];
        var unselectedBorder = isDark ? (Color)resources["InputStrokeDark"] : (Color)resources["InputStroke"];
        var selectedText = (Color)resources["White"];
        var unselectedText = isDark ? (Color)resources["TextPrimaryDark"] : (Color)resources["TextPrimary"];
        string[] labels = ["None", "Mild", "Moderate", "Severe"];

        var chips = PainChips;
        for (var i = 0; i < chips.Length; i++)
        {
            var selected = i == _viewModel.PainLevel;
            chips[i].BackgroundColor = selected ? selectedBg : unselectedBg;
            chips[i].BorderColor = selected ? selectedBg : unselectedBorder;
            chips[i].TextColor = selected ? selectedText : unselectedText;
            SemanticProperties.SetDescription(chips[i], $"Pain: {labels[i]}{(selected ? ", selected" : "")}");
        }
    }
}
