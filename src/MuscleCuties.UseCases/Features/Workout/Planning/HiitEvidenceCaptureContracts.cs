namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>The challenge the client must answer before HIIT evidence is accepted.</summary>
public sealed record HiitEvidenceCaptureChallengeDto(Guid ChallengeId, DateTimeOffset ExpiresAtUtc);

/// <summary>Issues a tenant-bound challenge for a fresh, explicit HIIT evidence capture.</summary>
public interface IHiitEvidenceCaptureStore
{
    Task<HiitEvidenceCaptureChallengeDto> IssueAsync(Guid userId, CancellationToken cancellationToken);
}
