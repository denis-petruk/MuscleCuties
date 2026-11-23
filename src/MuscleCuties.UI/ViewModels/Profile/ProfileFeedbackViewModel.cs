using System.Net.Mail;
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
    [ObservableProperty] private bool _isStatusError;
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
        BackCommand = new AsyncRelayCommand(BackAsync);
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        SendFeedbackCommand = new AsyncRelayCommand(SendFeedbackAsync);
        CopyFeedbackCommand = new AsyncRelayCommand(CopyFeedbackAsync);
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

    public bool ShowSubmitHint => !IsReadyToSend && !IsBusy;

    public bool HasUnsavedDraft =>
        !string.IsNullOrWhiteSpace(FeedbackText) ||
        !string.IsNullOrWhiteSpace(AdjustmentText) ||
        !string.IsNullOrWhiteSpace(ScreenName) ||
        HasAttachment;

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
    public AsyncRelayCommand CopyFeedbackCommand { get; }
    public RelayCommand RemoveAttachmentCommand { get; }
    public AsyncRelayCommand BackCommand { get; }

    private async Task BackAsync()
    {
        if (HasUnsavedDraft && Shell.Current is { } shell)
        {
            var discard = await shell.DisplayAlertAsync(
                UiTextCatalog.Get("Profile_UnsavedChanges_Title"),
                UiTextCatalog.Get("Profile_UnsavedChanges_Message"),
                UiTextCatalog.Get("Profile_UnsavedChanges_Discard"),
                UiTextCatalog.Get("Profile_UnsavedChanges_KeepEditing"));
            if (!discard)
                return;
        }

        await _navigateBackAsync();
    }

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
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    public void SetAttachmentError(string message)
    {
        AttachmentStatus = message;
    }

    private Task LoadDataAsync()
    {
        StatusMessage = string.Empty;
        IsStatusError = false;
        return Task.CompletedTask;
    }

    private async Task SendFeedbackAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(FeedbackText) && string.IsNullOrWhiteSpace(AdjustmentText))
        {
            IsStatusError = true;
            StatusMessage = "Ui_Profile_Feedback_Error_Describe";
            return;
        }

        if (IncludeContactEmail && !IsValidEmail(ContactEmail))
        {
            IsStatusError = true;
            StatusMessage = UiTextCatalog.Get("Profile_Feedback_InvalidEmail");
            return;
        }

        IsBusy = true;
        IsStatusError = false;
        try
        {
            var result = await _useCase.OpenDraftAsync(new ProfileFeedbackRequest(
                SelectedTopicLabel, SelectedPriorityLabel, ScreenName, FeedbackText,
                AdjustmentText, IncludeContactEmail, ContactEmail,
                _attachment));
            IsStatusError = !result.DraftOpened;
            StatusMessage = result.Message;
            if (result.DraftOpened)
                await _navigateBackAsync();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[ProfileFeedbackViewModel] Feedback handoff failed ({ex.GetType().Name}).");
            IsStatusError = true;
            StatusMessage = "Ui_Profile_Feedback_Error_NoMail";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CopyFeedbackAsync()
    {
        if (IsBusy || !HasUnsavedDraft)
            return;

        var lines = new List<string>
        {
            $"Topic: {SelectedTopicLabel}",
            $"Priority: {SelectedPriorityLabel}"
        };
        if (!string.IsNullOrWhiteSpace(ScreenName))
            lines.Add($"Screen: {ScreenName.Trim()}");
        if (!string.IsNullOrWhiteSpace(FeedbackText))
            lines.Add($"Message: {FeedbackText.Trim()}");
        if (!string.IsNullOrWhiteSpace(AdjustmentText))
            lines.Add($"Suggested fix: {AdjustmentText.Trim()}");
        if (IncludeContactEmail && !string.IsNullOrWhiteSpace(ContactEmail))
            lines.Add($"Reply email: {ContactEmail.Trim()}");

        await Clipboard.Default.SetTextAsync(string.Join(Environment.NewLine, lines));
        IsStatusError = false;
        StatusMessage = UiTextCatalog.Get("Profile_Feedback_Copied");
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var trimmed = value.Trim();
        return MailAddress.TryCreate(trimmed, out var parsed) &&
               string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase) &&
               trimmed.Contains('.', StringComparison.Ordinal);
    }

    partial void OnFeedbackTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
        OnPropertyChanged(nameof(ShowSubmitHint));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    partial void OnAdjustmentTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
        OnPropertyChanged(nameof(ShowSubmitHint));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    partial void OnScreenNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
        OnPropertyChanged(nameof(ShowSubmitHint));
    }

    private void RemoveAttachment()
    {
        _attachment = null;
        AttachmentName = string.Empty;
        AttachmentStatus = UiTextCatalog.Get("Profile_Feedback_AttachmentRemoved");
        OnPropertyChanged(nameof(HasAttachment));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }
}
