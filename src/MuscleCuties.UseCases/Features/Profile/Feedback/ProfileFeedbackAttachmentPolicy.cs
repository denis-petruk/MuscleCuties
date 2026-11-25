namespace MuscleCuties.UseCases.Features.Profile.Feedback;

/// <summary>The attachment limits, so an oversized report is refused before it is sent.</summary>
public static class ProfileFeedbackAttachmentPolicy
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".heic", ".pdf", ".txt", ".log"
    };

    /// <summary>Checks count, size and type of the attachments, so an oversized report is refused before sending.</summary>
    public static bool TryValidate(ProfileFeedbackAttachmentDto attachment, out string message)
    {
        if (string.IsNullOrWhiteSpace(attachment.FilePath))
        {
            message = "This file cannot be attached from this device.";
            return false;
        }

        if (!AllowedExtensions.Contains(Path.GetExtension(attachment.FileName)))
        {
            message = "Attach a screenshot, PDF, text, or log file.";
            return false;
        }

        if (attachment.SizeBytes <= 0)
        {
            message = "This file looks empty.";
            return false;
        }

        if (attachment.SizeBytes > MaxAttachmentBytes)
        {
            message = "Keep attachments under 10 MB so email can send smoothly.";
            return false;
        }

        message = "Attachment ready.";
        return true;
    }
}
