using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Profile;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UseCases.Features.Profile.Feedback;

namespace MuscleCuties.UI.ViewModels.Profile;

public partial class ProfileFeedbackViewModel : ObservableObject
{
    private readonly Func<Task> _navigateBackAsync;
    private readonly IProfileFeedbackUseCase _useCase;
    [ObservableProperty] private string _adjustmentText = string.Empty;
    private ProfileFeedbackAttachmentDto? _attachment;
    [ObservableProperty] private string _attachmentName = string.Empty;
    [ObservableProperty] private string _attachmentStatus = string.Empty;

    [ObservableProperty] private string _contactEmail = string.Empty;
    [ObservableProperty] private string _feedbackText = string.Empty;
    [ObservableProperty] private bool _includeContactEmail;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _screenName = string.Empty;
    private SelectionOption<FeedbackPriority>? _selectedPriorityOption;
    private SelectionOption<FeedbackTopic>? _selectedTopicOption;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ProfileFeedbackViewModel(
        IProfileFeedbackUseCase useCase,
        Func<Task> navigateBackAsync)
    {
        _useCase = useCase;
        _navigateBackAsync = navigateBackAsync;
        BackCommand = new AsyncRelayCommand(navigateBackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SendFeedbackCommand = new AsyncRelayCommand(SendFeedbackAsync);
        RemoveAttachmentCommand = new RelayCommand(RemoveAttachment);
        SelectedTopicOption = TopicOptions[0];
        SelectedPriorityOption = PriorityOptions[0];
    }

    public IReadOnlyList<SelectionOption<FeedbackTopic>> TopicOptions { get; } =
    [
        new(FeedbackTopic.DesignOrStyle, "Design or style", "SparkleCircle24"),
        new(FeedbackTopic.SomethingBroken, "Something broken", "Bug24"),
        new(FeedbackTopic.Nutrition, "Nutrition", "Food24"),
        new(FeedbackTopic.Workout, "Workout", "Dumbbell24"),
        new(FeedbackTopic.CycleTracking, "Cycle tracking", "HeartCircle24"),
        new(FeedbackTopic.Onboarding, "Onboarding", "PersonPill24"),
        new(FeedbackTopic.NewIdea, "New idea", "ChatBubblesQuestion24")
    ];

    public IReadOnlyList<SelectionOption<FeedbackPriority>> PriorityOptions { get; } =
    [
        new(FeedbackPriority.NiceToImprove, "Nice to improve", "SparkleCircle24"),
        new(FeedbackPriority.Annoying, "Annoying", "ChatWarning24"),
        new(FeedbackPriority.BlockingMe, "Blocking me", "ShieldError24"),
        new(FeedbackPriority.TinyPolish, "Tiny polish", "TargetEdit24")
    ];

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
        var attachment = new ProfileFeedbackAttachmentDto(fileName, filePath,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            sizeBytes);
        if (!ProfileFeedbackAttachmentPolicy.TryValidate(attachment, out var message))
        {
            AttachmentStatus = message;
            return;
        }

        _attachment = attachment;
        AttachmentName = fileName;
        AttachmentStatus = message;
        OnPropertyChanged(nameof(HasAttachment));
    }

    public void SetAttachmentError(string message)
    {
        AttachmentStatus = message;
    }

    private Task LoadDataAsync()
    {
        StatusMessage = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SendFeedbackAsync()
    {
        if (string.IsNullOrWhiteSpace(FeedbackText) && string.IsNullOrWhiteSpace(AdjustmentText))
        {
            StatusMessage = "Describe an issue or idea before opening the email draft.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _useCase.OpenDraftAsync(new ProfileFeedbackRequest(
                SelectedTopicLabel, SelectedPriorityLabel, ScreenName, FeedbackText,
                AdjustmentText, IncludeContactEmail, ContactEmail,
                _attachment));
            StatusMessage = result.Message;
            if (result.DraftOpened)
                await _navigateBackAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileFeedbackViewModel] Feedback handoff failed ({ex.GetType().Name}).");
            StatusMessage = "Could not open email on this device. Please try again from a device with mail set up.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnFeedbackTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
    }

    partial void OnAdjustmentTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
    }

    private void RemoveAttachment()
    {
        _attachment = null;
        AttachmentName = string.Empty;
        AttachmentStatus = "Attachment removed.";
        OnPropertyChanged(nameof(HasAttachment));
    }
}
