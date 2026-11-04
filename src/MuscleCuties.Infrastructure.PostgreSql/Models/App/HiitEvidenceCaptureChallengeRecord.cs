namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

public sealed class HiitEvidenceCaptureChallengeRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateOnly IssuedLocalDate { get; set; }
    public long TimeZoneVersion { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string? ConsumedRequestHash { get; set; }
    public long? ResultAnswerVersion { get; set; }
    public bool? ResultSafetyPlanChanged { get; set; }
}
