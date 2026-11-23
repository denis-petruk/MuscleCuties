

namespace MuscleCuties.Infrastructure.Cloud;

internal sealed record CloudEmailChallengeStart(string? ChallengeId, int ResendCooldownSeconds, int ExpiresInSeconds);
internal sealed record CloudEmailChallengeVerification(bool Verified, string? VerificationToken, int ReceiptExpiresInSeconds);
public sealed record CloudProfileWriteResponse(int? Version);
