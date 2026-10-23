namespace MuscleCuties.UseCases.Features.Profile.Feedback;

public sealed record ProfileFeedbackAttachmentDto(
    string FileName,
    string FilePath,
    string ContentType,
    long SizeBytes);

public sealed record ProfileFeedbackRequest(
    string Topic,
    string Priority,
    string ScreenName,
    string FeedbackText,
    string AdjustmentText,
    bool IncludeContactEmail,
    string ContactEmail,
    ProfileFeedbackAttachmentDto? Attachment);

public sealed record ProfileFeedbackResult(bool DraftOpened, string Message);

public interface IProfileFeedbackComposer
{
    Task OpenDraftAsync(string subject, string body,
        ProfileFeedbackAttachmentDto? attachment, CancellationToken cancellationToken);
}

public interface IProfileFeedbackUseCase
{
    Task<ProfileFeedbackResult> OpenDraftAsync(
        ProfileFeedbackRequest request, CancellationToken cancellationToken = default);
}
