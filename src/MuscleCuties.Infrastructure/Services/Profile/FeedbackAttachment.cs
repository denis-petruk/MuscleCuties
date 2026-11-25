namespace MuscleCuties.Core.Services.Profile;

/// <summary>One attachment on a feedback report: its file, name and type.</summary>
public sealed record FeedbackAttachment(
    string FileName,
    string FilePath,
    string ContentType,
    long SizeBytes);
