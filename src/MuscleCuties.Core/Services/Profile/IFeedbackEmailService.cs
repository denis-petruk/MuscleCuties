namespace MuscleCuties.Core.Services.Profile;

public interface IFeedbackEmailService
{
    Task SendFeedbackAsync(
        string subject,
        string body,
        IReadOnlyList<FeedbackAttachment>? attachments = null);
}
