using Microsoft.Maui.ApplicationModel.Communication;
using MuscleCuties.Core.Services.Profile;

namespace MuscleCuties.App.Services;

public sealed class FeedbackEmailService : IFeedbackEmailService
{
    private const string FeedbackRecipient = "deniska.petruk@icloud.com";

    public async Task SendFeedbackAsync(string subject, string body)
    {
        var message = new EmailMessage
        {
            Subject = subject,
            Body = body,
            To = [FeedbackRecipient]
        };

        await Email.Default.ComposeAsync(message);
    }
}
