using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.HealthSync;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileHealthSyncViewModel : ObservableObject
{
    private readonly IProfileRefreshNotifier _refreshNotifier;
    private readonly IProfileHealthSyncUseCase _useCase;
    [ObservableProperty] private string _connectionBadgeText = "Tap to Connect";
    [ObservableProperty] private bool _isAppleHealthAvailable;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConnected;

    [ObservableProperty] private string _lastSyncedText = "No sync yet";

    [ObservableProperty]
    private string _readinessExplanationText = "This check-in score is a planning estimate based on your answers, not a direct measurement of recovery or a medical assessment.";

    [ObservableProperty] private int _readinessScore;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _statusText = "Not connected";

    public ProfileHealthSyncViewModel(
        IProfileHealthSyncUseCase useCase,
        IProfileRefreshNotifier refreshNotifier,
        Func<Task> navigateBackAsync)
    {
        _useCase = useCase;
        _refreshNotifier = refreshNotifier;
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
    }

    public string ReadinessScoreText => ReadinessScore > 0
        ? ReadinessScore.ToString(CultureInfo.InvariantCulture)
        : "Unavailable";

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    private async Task ConnectAsync()
    {
        if (!IsAppleHealthAvailable || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var result = await _useCase.ConnectAsync();
            if (result.IsConnected)
                _refreshNotifier.InvalidateDashboard();
            StatusMessage = result.IsConnected ? "Step counts refreshed." : result.Message;
            ApplyStatus(result.Summary);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisconnectAsync()
    {
        if (!IsConnected || IsBusy)
            return;

        IsBusy = true;
        try
        {
            var result = await _useCase.DisconnectAsync();
            ApplyStatus(result.Summary);
            StatusMessage = result.Message;
            _refreshNotifier.InvalidateDashboard();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            ApplyStatus(await _useCase.LoadAsync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyStatus(ProfileHealthSyncDto summary)
    {
        IsAppleHealthAvailable = summary.IsAppleHealthAvailable;
        IsConnected = summary.IsConnected;
        StatusText = summary.StatusText;
        ConnectionBadgeText = summary.ConnectionBadgeText;
        LastSyncedText = summary.LastSyncedText;
        ReadinessScore = summary.ReadinessScore;
        ReadinessExplanationText = summary.ReadinessExplanationText;
    }

    partial void OnReadinessScoreChanged(int value)
    {
        OnPropertyChanged(nameof(ReadinessScoreText));
    }
}
