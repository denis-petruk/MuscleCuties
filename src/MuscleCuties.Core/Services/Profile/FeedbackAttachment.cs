namespace MuscleCuties.Core.Services.Profile;

public sealed record FeedbackAttachment(
    string FileName,
    string FilePath,
    string ContentType,
    long SizeBytes);
