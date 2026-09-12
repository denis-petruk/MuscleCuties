using MuscleCuties.Core.ViewModels.Dashboard;

namespace MuscleCuties.App.Pages.Dashboard;

public partial class DailyCheckInPage : ContentPage
{
    private readonly DailyCheckInViewModel _viewModel;
    private bool _hasAnimated;

    private Border[] EnergyChips => [EnergyChip1, EnergyChip2, EnergyChip3, EnergyChip4, EnergyChip5];
    private Border[] PainChips => [PainChip0, PainChip1, PainChip2, PainChip3];

    public DailyCheckInPage(DailyCheckInViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = vm;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(LoadPageAsync);
    }

    private async Task LoadPageAsync()
    {
        await _viewModel.CheckIfAlreadyCompletedAsync();
        UpdateEnergyChips();
        UpdatePainChips();

        if (!_hasAnimated)
        {
            _hasAnimated = true;
            PlayEntranceAnimations();
        }
    }

    private void PlayEntranceAnimations()
    {
        var cards = new List<VisualElement>
            { HeaderCard, SleepCard, EnergyCard, PainCard, BloatingCard, WeightCard };

        if (_viewModel.IsCompleted)
            cards.Add(ResultCard);

        foreach (var card in cards)
        {
            card.Opacity = 0;
            card.TranslationY = 30;
        }

        ActionArea.Opacity = 0;
        ActionArea.TranslationY = 20;

        Dispatcher.Dispatch(async () =>
        {
            try
            {
                for (var i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    _ = card.FadeToAsync(1, 300, Easing.CubicOut);
                    _ = card.TranslateToAsync(0, 0, 350, Easing.CubicOut);
                    await Task.Delay(80);
                }

                await Task.WhenAll(
                    ActionArea.FadeToAsync(1, 300, Easing.CubicOut),
                    ActionArea.TranslateToAsync(0, 0, 300, Easing.CubicOut));
            }
            catch
            {
                foreach (var card in cards)
                {
                    card.Opacity = 1;
                    card.TranslationY = 0;
                }
                ActionArea.Opacity = 1;
                ActionArea.TranslationY = 0;
            }
        });
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DailyCheckInViewModel.IsCompleted) && _viewModel.IsCompleted)
            PlayResultReveal();
    }

    private void PlayResultReveal()
    {
        ResultCard.Opacity = 0;
        ResultCard.TranslationY = 30;
        ResultCard.Scale = 0.95;

        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await Task.WhenAll(
                    ResultCard.FadeToAsync(1, 350, Easing.CubicOut),
                    ResultCard.TranslateToAsync(0, 0, 400, Easing.CubicOut),
                    ResultCard.ScaleToAsync(1, 400, Easing.CubicOut));
            }
            catch
            {
                ResultCard.Opacity = 1;
                ResultCard.TranslationY = 0;
                ResultCard.Scale = 1;
            }
        });
    }

    private void OnEnergyTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string levelStr && int.TryParse(levelStr, out var level))
        {
            _viewModel.EnergyLevel = level;
            UpdateEnergyChips();
        }
    }

    private void OnPainTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string levelStr && int.TryParse(levelStr, out var level))
        {
            _viewModel.PainLevel = level;
            UpdatePainChips();
        }
    }

    private void UpdateEnergyChips()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var resources = Application.Current!.Resources;
        var selectedBg = (Color)resources["Primary"];
        var unselectedBg = isDark ? (Color)resources["Gray800"] : (Color)resources["Gray200"];
        var selectedText = (Color)resources["White"];
        var unselectedText = isDark ? (Color)resources["TextSecondaryDark"] : (Color)resources["TextSecondary"];

        var chips = EnergyChips;
        for (var i = 0; i < chips.Length; i++)
        {
            var selected = (i + 1) == _viewModel.EnergyLevel;
            chips[i].BackgroundColor = selected ? selectedBg : unselectedBg;
            if (chips[i].Content is Label label)
                label.TextColor = selected ? selectedText : unselectedText;
        }
    }

    private void UpdatePainChips()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var resources = Application.Current!.Resources;
        var selectedBg = (Color)resources["Primary"];
        var unselectedBg = isDark ? (Color)resources["Gray800"] : (Color)resources["Gray200"];
        var selectedText = (Color)resources["White"];
        var unselectedText = isDark ? (Color)resources["TextSecondaryDark"] : (Color)resources["TextSecondary"];

        var chips = PainChips;
        for (var i = 0; i < chips.Length; i++)
        {
            var selected = i == _viewModel.PainLevel;
            chips[i].BackgroundColor = selected ? selectedBg : unselectedBg;
            if (chips[i].Content is Label label)
                label.TextColor = selected ? selectedText : unselectedText;
        }
    }
}
