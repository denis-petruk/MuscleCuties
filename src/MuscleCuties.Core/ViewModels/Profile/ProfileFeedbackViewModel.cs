using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Profile;
using MuscleCuties.Core.Models.UI.Common;
using MuscleCuties.Core.Models.UI.Profile;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Profile;

namespace MuscleCuties.Core.ViewModels.Profile;

public partial class ProfileFeedbackViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IFeedbackEmailService _feedbackEmailService;
    private readonly Func<Task> _navigateBackAsync;
    private readonly IUserRepository _userRepository;
    [ObservableProperty] private string _adjustmentText = string.Empty;
    private FeedbackAttachment? _attachment;
    [ObservableProperty] private string _attachmentName = string.Empty;
    [ObservableProperty] private string _attachmentStatus = string.Empty;

    [ObservableProperty] private string _contactEmail = string.Empty;
    [ObservableProperty] private string _feedbackText = string.Empty;
    [ObservableProperty] private bool _includeContactEmail = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _screenName = string.Empty;
    private SelectionOption<FeedbackPriority>? _selectedPriorityOption;
    private SelectionOption<FeedbackTopic>? _selectedTopicOption;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ProfileFeedbackViewModel(
        IAuthService authService,
        IUserRepository userRepository,
        IFeedbackEmailService feedbackEmailService,
        Func<Task> navigateBackAsync)
    {
        _authService = authService;
        _userRepository = userRepository;
        _feedbackEmailService = feedbackEmailService;
        _navigateBackAsync = navigateBackAsync;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SendFeedbackCommand = new AsyncRelayCommand(SendFeedbackAsync);
        RemoveAttachmentCommand = new RelayCommand(RemoveAttachment);
        BackCommand = new AsyncRelayCommand(_navigateBackAsync);
        SelectedTopicOption = TopicOptions.First();
        SelectedPriorityOption = PriorityOptions.First();
    }

    public IReadOnlyList<SelectionOption<FeedbackTopic>> TopicOptions { get; } =
        ProfileSelectionOptions.FeedbackTopics;

    public IReadOnlyList<SelectionOption<FeedbackPriority>> PriorityOptions { get; } =
        ProfileSelectionOptions.FeedbackPriorities;

    public string FeedbackCountText => $"{FeedbackText.Length + AdjustmentText.Length} characters";

    public bool IsReadyToSend =>
        !IsBusy && (!string.IsNullOrWhiteSpace(FeedbackText) || !string.IsNullOrWhiteSpace(AdjustmentText));

    public bool HasAttachment => _attachment is not null;
    public string SelectedTopicLabel => SelectedTopicOption?.Label ?? "Design or style";
    public string SelectedPriorityLabel => SelectedPriorityOption?.Label ?? "Nice to improve";

    public SelectionOption<FeedbackTopic>? SelectedTopicOption
    {
        get => _selectedTopicOption;
        set
        {
            if (!SetProperty(ref _selectedTopicOption, value))
                return;

            OnPropertyChanged(nameof(SelectedTopicLabel));
        }
    }

    public SelectionOption<FeedbackPriority>? SelectedPriorityOption
    {
        get => _selectedPriorityOption;
        set
        {
            if (!SetProperty(ref _selectedPriorityOption, value))
                return;

            OnPropertyChanged(nameof(SelectedPriorityLabel));
        }
    }

    public AsyncRelayCommand LoadDataCommand { get; }
    public AsyncRelayCommand SendFeedbackCommand { get; }
    public RelayCommand RemoveAttachmentCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    public void AttachFile(
        string fileName,
        string filePath,
        string? contentType,
        long sizeBytes)
    {
        if (!FeedbackAttachmentPolicy.TryValidate(fileName, filePath, sizeBytes, out var message))
        {
            AttachmentStatus = message;
            return;
        }

        _attachment = new FeedbackAttachment(
            fileName,
            filePath,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            sizeBytes);
        AttachmentName = fileName;
        AttachmentStatus = message;
        OnPropertyChanged(nameof(HasAttachment));
    }

    public void SetAttachmentError(string message)
    {
        AttachmentStatus = message;
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
            var attachmentText = _attachment is null
                ? "None"
                : $"{_attachment.FileName} ({_attachment.SizeBytes / 1024f:N1} KB)";
            var body =
                $"From: {name}\n" +
                $"Contact email: {email}\n" +
                $"Topic: {SelectedTopicLabel}\n" +
                $"Priority: {SelectedPriorityLabel}\n" +
                $"Screen or flow: {NormalizeOptional(ScreenName)}\n" +
                $"Attachment: {attachmentText}\n" +
                $"Created at: {DateTime.Now:g}\n\n" +
                "Feedback:\n" +
                $"{FeedbackText.Trim()}\n\n" +
                "Requested adjustments:\n" +
                $"{AdjustmentText.Trim()}\n\n" +
                "Private beta feedback for the handsome, jacked developer only.";

            IReadOnlyList<FeedbackAttachment> attachments = _attachment is null
                ? Array.Empty<FeedbackAttachment>()
                : [_attachment];
            await _feedbackEmailService.SendFeedbackAsync($"MuscleCuties beta feedback - {SelectedTopicLabel}", body,
                attachments);
            StatusMessage = "Feedback email is ready to send.";
            await _navigateBackAsync();
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

    private static string NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not specified" : value.Trim();
    }

    private void RemoveAttachment()
    {
        _attachment = null;
        AttachmentName = string.Empty;
        AttachmentStatus = "Attachment removed.";
        OnPropertyChanged(nameof(HasAttachment));
    }
}
