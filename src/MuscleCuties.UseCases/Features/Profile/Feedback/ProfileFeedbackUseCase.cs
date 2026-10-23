namespace MuscleCuties.UseCases.Features.Profile.Feedback;

public sealed class ProfileFeedbackUseCase(IProfileFeedbackComposer composer) : IProfileFeedbackUseCase
{
    public async Task<ProfileFeedbackResult> OpenDraftAsync(
        ProfileFeedbackRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FeedbackText) &&
            string.IsNullOrWhiteSpace(request.AdjustmentText))
            return new ProfileFeedbackResult(false, "Describe an issue or idea before opening the email draft.");

        var topic = Optional(request.Topic);
        var email = request.IncludeContactEmail && !string.IsNullOrWhiteSpace(request.ContactEmail)
            ? request.ContactEmail.Trim()
            : "Not included";
        var attachment = request.Attachment;
        var attachmentText = attachment is null
            ? "None"
            : $"{attachment.FileName} ({attachment.SizeBytes / 1024f:N1} KB)";
        var body =
            $"Contact email: {email}\n" +
            $"Topic: {topic}\n" +
            $"Priority: {Optional(request.Priority)}\n" +
            $"Screen or flow: {Optional(request.ScreenName)}\n" +
            $"Attachment: {attachmentText}\n" +
            $"Created at: {DateTime.Now:g}\n\n" +
            "Feedback:\n" +
            $"{request.FeedbackText.Trim()}\n\n" +
            "Requested adjustments:\n" +
            $"{request.AdjustmentText.Trim()}\n\n" +
            "MuscleCuties beta feedback.";

        await composer.OpenDraftAsync($"MuscleCuties beta feedback - {topic}", body,
            attachment, cancellationToken).ConfigureAwait(false);
        return new ProfileFeedbackResult(true, "Feedback email is ready to send.");
    }

    private static string Optional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not specified" : value.Trim();
    }
}
