namespace MuscleCuties.UseCases.Features.Profile.Feedback;

/// <summary>One attachment on a feedback report.</summary>
public sealed record ProfileFeedbackAttachmentDto(
    string FileName,
    string FilePath,
    string ContentType,
    long SizeBytes);

/// <summary>A feedback report: its text and attachments.</summary>
public sealed record ProfileFeedbackRequest(
    string Topic,
    string Priority,
    string ScreenName,
    string FeedbackText,
    string AdjustmentText,
    bool IncludeContactEmail,
    string ContactEmail,
    ProfileFeedbackAttachmentDto? Attachment);

/// <summary>Whether the report was sent, and why not when it was not.</summary>
public sealed record ProfileFeedbackResult(bool DraftOpened, string Message);

/// <summary>Composes the report for the transport.</summary>
public interface IProfileFeedbackComposer
{
    Task OpenDraftAsync(string subject, string body,
        ProfileFeedbackAttachmentDto? attachment, CancellationToken cancellationToken);
}

/// <summary>Opens the user's mail draft with the composed feedback report.</summary>
public interface IProfileFeedbackUseCase
{
    Task<ProfileFeedbackResult> OpenDraftAsync(
        ProfileFeedbackRequest request, CancellationToken cancellationToken = default);
}
