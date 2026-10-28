namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

public sealed class StrengthStyleOfferRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid RevisionId { get; set; }
    public int CurrentStyleId { get; set; }
    public int[] AlternativeStyleIds { get; set; } = [];
    public DateTime? DismissedAtUtc { get; set; }
}
