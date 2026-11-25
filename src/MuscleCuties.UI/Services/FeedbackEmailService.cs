using Microsoft.Maui.ApplicationModel.Communication;
using MuscleCuties.Core.Services.Profile;

namespace MuscleCuties.UI.Services;

/// <summary>Opens the platform mail composer with the app's feedback address already filled in;</summary>
/// the person still sends the message themselves.</summary>
public sealed class FeedbackEmailService : IFeedbackEmailService
{
    private const string FeedbackRecipient = "deniska.petruk@icloud.com";

    /// <summary>Opens the mail composer with the feedback and attachments.</summary>
    public async Task SendFeedbackAsync(
        string subject,
        string body,
        IReadOnlyList<FeedbackAttachment>? attachments = null)
    {
        var message = new EmailMessage
        {
            Subject = subject,
            Body = body,
            To = [FeedbackRecipient],
            Attachments = []
        };

        foreach (var attachment in attachments ?? [])
            message.Attachments.Add(new EmailAttachment(attachment.FilePath, attachment.ContentType));

        await Email.Default.ComposeAsync(message);
    }
}
