namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

/// <summary>A HIIT capture challenge: what was asked, and what answering it produced.</summary>
public sealed class HiitEvidenceCaptureChallengeRecord : ITenantRecord
{
    /// <summary>Primary key of the challenge.</summary>
    public Guid Id { get; set; }
    /// <summary>The account the capture is for.</summary>
    public Guid UserId { get; set; }
    /// <summary>When the challenge was issued.</summary>
    public DateTime IssuedAtUtc { get; set; }
    /// <summary>After this the capture is refused, which bounds how stale the evidence can be.</summary>
    public DateTime ExpiresAtUtc { get; set; }
    /// <summary>The local day it was issued for, so a day's capture can only be answered once.</summary>
    public DateOnly IssuedLocalDate { get; set; }
    /// <summary>The tz version that local date was resolved against.</summary>
    public long TimeZoneVersion { get; set; }
    /// <summary>When it was answered; null while still open.</summary>
    public DateTime? ConsumedAtUtc { get; set; }
    /// <summary>Hash of the answering request, so a replayed answer is detected rather than applied twice.</summary>
    public string? ConsumedRequestHash { get; set; }
    /// <summary>The answer revision the capture produced, so a later check-in can tell it is stale.</summary>
    public long? ResultAnswerVersion { get; set; }
    /// <summary>Whether answering the capture changed the safety plan, kept because the client must react to it.</summary>
    public bool? ResultSafetyPlanChanged { get; set; }
}
