

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>The start of an email change challenge, carrying the challenge that must be confirmed.</summary>
internal sealed record CloudEmailChallengeStart(string? ChallengeId, int ResendCooldownSeconds, int ExpiresInSeconds);
/// <summary>Whether an email change challenge was confirmed.</summary>
internal sealed record CloudEmailChallengeVerification(bool Verified, string? VerificationToken, int ReceiptExpiresInSeconds);
/// <summary>A profile write's answer: the new version, which the next edit has to send back.</summary>
public sealed record CloudProfileWriteResponse(int? Version);
