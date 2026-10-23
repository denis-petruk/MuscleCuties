namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

public sealed class PhotoUploadIntentRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long DeclaredSizeBytes { get; set; }
    public string Sha256Checksum { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string State { get; set; } = "Pending";
}

public sealed class PhotoObjectCleanupRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PhotoId { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime NotBeforeUtc { get; set; }
    public string State { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public Guid? LeaseToken { get; set; }
    public long LeaseGeneration { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? LastFailureCategory { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Owner-independent durable S3 cleanup identity. Contains only a generated opaque object key.</summary>
public sealed class PhotoObjectReconcileTargetRecord
{
    public Guid Id { get; set; }
    public string TargetKey { get; set; } = string.Empty;
    public DateTime NotBeforeUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public long LeaseGeneration { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
}
