namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

/// <summary>The strength style offered for one plan revision, and whether she dismissed it.</summary>
public sealed class StrengthStyleOfferRecord : ITenantRecord
{
    /// <summary>Primary key of the offer.</summary>
    public Guid Id { get; set; }
    /// <summary>The account the offer was made to.</summary>
    public Guid UserId { get; set; }
    /// <summary>The plan the offer belongs to.</summary>
    public Guid PlanId { get; set; }
    /// <summary>The exact revision offered, so a newer revision makes the offer stale rather than silently reused.</summary>
    public Guid RevisionId { get; set; }
    /// <summary>The style the plan is currently built with.</summary>
    public int CurrentStyleId { get; set; }
    /// <summary>The styles she may switch to, stored as an array so one read returns the whole offer.</summary>
    public int[] AlternativeStyleIds { get; set; } = [];
    /// <summary>When she dismissed it; a dismissed offer is not shown again for that revision.</summary>
    public DateTime? DismissedAtUtc { get; set; }
}
