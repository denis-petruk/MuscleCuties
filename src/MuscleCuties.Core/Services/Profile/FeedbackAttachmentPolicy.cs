using System.IO;

namespace MuscleCuties.Core.Services.Profile;

public static class FeedbackAttachmentPolicy
{
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".heic",
        ".pdf",
        ".txt",
        ".log"
    };

    public static bool TryValidate(
        string fileName,
        string filePath,
        long sizeBytes,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            message = "This file cannot be attached from this device.";
            return false;
        }

        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            message = "Attach a screenshot, PDF, text, or log file.";
            return false;
        }

        if (sizeBytes <= 0)
        {
            message = "This file looks empty.";
            return false;
        }

        if (sizeBytes > MaxAttachmentBytes)
        {
            message = "Keep attachments under 10 MB so email can send smoothly.";
            return false;
        }

        message = "Attachment ready.";
        return true;
    }
}
