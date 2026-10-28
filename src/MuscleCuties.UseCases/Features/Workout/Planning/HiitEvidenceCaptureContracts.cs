namespace MuscleCuties.UseCases.Features.Workout.Planning;

public sealed record HiitEvidenceCaptureChallengeDto(Guid ChallengeId, DateTimeOffset ExpiresAtUtc);

/// <summary>Issues a tenant-bound challenge for a fresh, explicit HIIT evidence capture.</summary>
public interface IHiitEvidenceCaptureStore
{
    Task<HiitEvidenceCaptureChallengeDto> IssueAsync(Guid userId, CancellationToken cancellationToken);
}
