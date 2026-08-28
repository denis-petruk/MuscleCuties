using Microsoft.Maui.ApplicationModel.Communication;
using MuscleCuties.Core.Services.Profile;

namespace MuscleCuties.App.Services;

public sealed class FeedbackEmailService : IFeedbackEmailService
{
    private const string FeedbackRecipient = "deniska.petruk@icloud.com";

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
