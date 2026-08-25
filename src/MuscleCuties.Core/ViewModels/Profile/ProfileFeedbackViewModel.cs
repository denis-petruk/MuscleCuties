using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Profile;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileFeedbackViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly IFeedbackEmailService _feedbackEmailService;
    private readonly Action _navigateBack;

    [ObservableProperty] private string _selectedTopic = "Design or style";
    [ObservableProperty] private string _selectedPriority = "Nice to improve";
    [ObservableProperty] private string _contactEmail = string.Empty;
    [ObservableProperty] private bool _includeContactEmail = true;
    [ObservableProperty] private string _screenName = string.Empty;
    [ObservableProperty] private string _feedbackText = string.Empty;
    [ObservableProperty] private string _adjustmentText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public IReadOnlyList<string> TopicOptions { get; } =
    [
        "Design or style",
        "Something broken",
        "Nutrition",
        "Workout",
        "Cycle tracking",
        "Onboarding",
        "New idea"
    ];

    public IReadOnlyList<string> PriorityOptions { get; } =
    [
        "Nice to improve",
        "Annoying",
        "Blocking me",
        "Tiny polish"
    ];

    public string FeedbackCountText => $"{FeedbackText.Length + AdjustmentText.Length} characters";
    public bool IsReadyToSend => !IsBusy && (!string.IsNullOrWhiteSpace(FeedbackText) || !string.IsNullOrWhiteSpace(AdjustmentText));

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SendFeedbackCommand { get; }
    public RelayCommand BackCommand { get; }

    public ProfileFeedbackViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        IFeedbackEmailService feedbackEmailService,
        Action navigateBack)
    {
        _authService = authService;
        _userRepository = userRepository;
        _feedbackEmailService = feedbackEmailService;
        _navigateBack = navigateBack;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SendFeedbackCommand = new AsyncRelayCommand(SendFeedbackAsync);
        BackCommand = new RelayCommand(_navigateBack);
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var user = await _userRepository.GetByIdAsync(userId);
            ContactEmail = user?.Email ?? string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SendFeedbackAsync()
    {
        if (string.IsNullOrWhiteSpace(FeedbackText) && string.IsNullOrWhiteSpace(AdjustmentText))
        {
            StatusMessage = "Give the handsome, jacked developer at least one clue first.";
            return;
        }

        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var user = await _userRepository.GetByIdAsync(userId);
            var profile = await _userRepository.GetProfileAsync(userId);
            var email = IncludeContactEmail
                ? string.IsNullOrWhiteSpace(ContactEmail) ? user?.Email ?? "Not provided" : ContactEmail.Trim()
                : "Tester chose not to include contact email";
            var name = string.IsNullOrWhiteSpace(profile?.Name) ? "Beta tester" : profile.Name;
            var body =
                $"From: {name}\n" +
                $"Contact email: {email}\n" +
                $"Topic: {SelectedTopic}\n" +
                $"Priority: {SelectedPriority}\n" +
                $"Screen or flow: {NormalizeOptional(ScreenName)}\n" +
                $"Created at: {DateTime.Now:g}\n\n" +
                "Feedback:\n" +
                $"{FeedbackText.Trim()}\n\n" +
                "Requested adjustments:\n" +
                $"{AdjustmentText.Trim()}\n\n" +
                "Private beta feedback for the handsome, jacked developer only.";

            await _feedbackEmailService.SendFeedbackAsync($"MuscleCuties beta feedback - {SelectedTopic}", body);
            StatusMessage = "Feedback email is ready to send.";
        }
        catch (Exception)
        {
            StatusMessage = "Could not open email on this device. Please try again from a device with mail set up.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnFeedbackTextChanged(string value)
    {
        OnPropertyChanged(nameof(FeedbackCountText));
        OnPropertyChanged(nameof(IsReadyToSend));
    }

    partial void OnAdjustmentTextChanged(string value)
    {
        OnPropertyChanged(nameof(FeedbackCountText));
        OnPropertyChanged(nameof(IsReadyToSend));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
    }

    private static string NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Not specified" : value.Trim();
}
