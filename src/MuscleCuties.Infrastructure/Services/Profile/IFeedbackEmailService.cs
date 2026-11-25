namespace MuscleCuties.Core.Services.Profile;

/// <summary>Sends a feedback report on the user's behalf.</summary>
public interface IFeedbackEmailService
{
    Task SendFeedbackAsync(
        string subject,
        string body,
        IReadOnlyList<FeedbackAttachment>? attachments = null);
}
