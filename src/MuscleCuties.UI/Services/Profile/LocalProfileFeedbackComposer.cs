using MuscleCuties.Core.Services.Profile;
using MuscleCuties.UseCases.Features.Profile.Feedback;

namespace MuscleCuties.UI.Services.Profile;

public sealed class LocalProfileFeedbackComposer(IFeedbackEmailService emailService)
    : IProfileFeedbackComposer
{
    public Task OpenDraftAsync(string subject, string body,
        ProfileFeedbackAttachmentDto? attachment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (attachment is not null)
        {
            if (!ProfileFeedbackAttachmentPolicy.TryValidate(attachment, out var error))
                throw new InvalidOperationException(error);
            if (IsWithinAppStorage(attachment.FilePath))
                throw new InvalidOperationException("Private app files cannot be attached to feedback.");
        }

        IReadOnlyList<FeedbackAttachment> attachments = attachment is null
            ? []
            :
            [
                new FeedbackAttachment(attachment.FileName, attachment.FilePath,
                    attachment.ContentType, attachment.SizeBytes)
            ];
        return emailService.SendFeedbackAsync(subject, body, attachments);
    }

    private static bool IsWithinAppStorage(string filePath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(FileSystem.AppDataDirectory));
        var candidate = Path.GetFullPath(filePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(candidate, root, comparison) ||
               candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }
}
