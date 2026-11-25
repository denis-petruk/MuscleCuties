using System.Net.Mail;
using MuscleCuties.Core.Models.Enums.Profile;
using MuscleCuties.UI.Models.UI.Common;
using MuscleCuties.UseCases.Features.Profile.Feedback;

namespace MuscleCuties.UI.ViewModels.Profile;

/// <summary>
///     Feedback form state: draft text, the contact opt-in, and the submission result.
/// </summary>
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

    /// <summary>Takes the feedback use case and the back navigation callback.</summary>
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

    /// <summary>The feedback topics to choose from.</summary>
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

    /// <summary>The priorities to choose from.</summary>
    public IReadOnlyList<SelectionOption<FeedbackPriority>> PriorityOptions { get; } =
    [
        new(FeedbackPriority.NiceToImprove, "Nice to improve", "SparkleCircle24"),
        new(FeedbackPriority.Annoying, "Annoying", "ChatWarning24"),
        new(FeedbackPriority.BlockingMe, "Blocking me", "ShieldError24"),
        new(FeedbackPriority.TinyPolish, "Tiny polish", "TargetEdit24")
    ];

    /// <summary>Send needs a message or an adjustment and no send in flight; the topic and priority already have</summary>
    /// defaults, so they are never what stops a report.</summary>
    public bool IsReadyToSend =>
        !IsBusy && (!string.IsNullOrWhiteSpace(FeedbackText) || !string.IsNullOrWhiteSpace(AdjustmentText));

    /// <summary>True when the submit hint is shown.</summary>
    public bool ShowSubmitHint => !IsReadyToSend && !IsBusy;

    /// <summary>True when the draft has unsent content.</summary>
    public bool HasUnsavedDraft =>
        !string.IsNullOrWhiteSpace(FeedbackText) ||
        !string.IsNullOrWhiteSpace(AdjustmentText) ||
        !string.IsNullOrWhiteSpace(ScreenName) ||
        HasAttachment;

    /// <summary>True when a file is attached.</summary>
    public bool HasAttachment => _attachment is not null;
    /// <summary>The chosen topic's name, falling back to the default so the chip never reads empty.</summary>
    public string SelectedTopicLabel => SelectedTopicOption?.Label ?? "Design or style";
    /// <summary>The chosen priority's name, with the same default.</summary>
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

    /// <summary>Loads the draft.</summary>
    public AsyncRelayCommand LoadDataCommand { get; }
    /// <summary>Sends the feedback.</summary>
    public AsyncRelayCommand SendFeedbackCommand { get; }
    /// <summary>Copies the feedback to the clipboard.</summary>
    public AsyncRelayCommand CopyFeedbackCommand { get; }
    /// <summary>Removes the attachment.</summary>
    public RelayCommand RemoveAttachmentCommand { get; }
    /// <summary>Leaves the page, warning about the draft.</summary>
    public AsyncRelayCommand BackCommand { get; }

    /// <summary>Returns to the previous page, dropping the draft when confirmed.</summary>
    private async Task BackAsync() =>
        await ProfileNavigationGuard.TryNavigateBackAsync(HasUnsavedDraft, ProfileDiscardPrompt.ConfirmAsync,
            _navigateBackAsync);

    /// <summary>Attaches the chosen file.</summary>
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

    /// <summary>Reports why an attachment was refused, through the same status line the rest of the page uses.</summary>
    public void SetAttachmentError(string message)
    {
        AttachmentStatus = message;
    }

    /// <summary>The page has nothing to read, so opening it only clears the status left by a previous visit.</summary>
    private Task LoadDataAsync()
    {
        StatusMessage = string.Empty;
        IsStatusError = false;
        return Task.CompletedTask;
    }

    /// <summary>Submits the report with its attachment, and keeps the draft when the submission is refused so the user can</summary>
    /// retry it.</summary>
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

    /// <summary>Puts the report on the clipboard as plain text, for a user who would rather paste it somewhere else.</summary>
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

    /// <summary>A shape check for the optional reply address, so a typo is caught before the report is sent.</summary>
    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var trimmed = value.Trim();
        return MailAddress.TryCreate(trimmed, out var parsed) &&
               string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase) &&
               trimmed.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>Refreshes the send state when the text changes.</summary>
    partial void OnFeedbackTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
        OnPropertyChanged(nameof(ShowSubmitHint));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    /// <summary>Refreshes the send state when the adjustment changes.</summary>
    partial void OnAdjustmentTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
        OnPropertyChanged(nameof(ShowSubmitHint));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    /// <summary>Refreshes the draft when the name changes.</summary>
    partial void OnScreenNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }

    /// <summary>Refreshes the command states when busy changes.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadyToSend));
        OnPropertyChanged(nameof(ShowSubmitHint));
    }

    /// <summary>Drops the attachment and its name, which also makes the draft dirty or clean again through the</summary>
    /// attachment flag.</summary>
    private void RemoveAttachment()
    {
        _attachment = null;
        AttachmentName = string.Empty;
        AttachmentStatus = UiTextCatalog.Get("Profile_Feedback_AttachmentRemoved");
        OnPropertyChanged(nameof(HasAttachment));
        OnPropertyChanged(nameof(HasUnsavedDraft));
    }
}
