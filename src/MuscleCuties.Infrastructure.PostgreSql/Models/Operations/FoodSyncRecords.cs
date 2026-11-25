namespace MuscleCuties.Infrastructure.PostgreSql.Models.Operations;

/// <summary>A nutrient snapshot taken before a catalog food changed, so a sync's effect can be diffed.</summary>
public sealed class FoodItemVersionRecord
{
    /// <summary>Primary key of the snapshot.</summary>
    public int Id { get; set; }
    /// <summary>The catalog food this snapshot belongs to.</summary>
    public int FoodItemId { get; set; }
    /// <summary>The nutrient values as they were, in the same shape the live row stores.</summary>
    public string NutrientJson { get; set; } = string.Empty;
    /// <summary>When the snapshot was taken.</summary>
    public DateTime VersionedAtUtc { get; set; }
    /// <summary>What caused the change, so a sync can be told apart from a manual edit.</summary>
    public string ChangeSource { get; set; } = string.Empty;
}

/// <summary>One catalog sync run and what it wrote or failed on.</summary>
public sealed class FoodSyncLogRecord
{
    /// <summary>Primary key of the run.</summary>
    public int Id { get; set; }
    /// <summary>When the run started.</summary>
    public DateTime StartedAtUtc { get; set; }
    /// <summary>When it finished; null while it is still running.</summary>
    public DateTime? CompletedAtUtc { get; set; }
    /// <summary>Rows written by the run.</summary>
    public int ItemsUpserted { get; set; }
    /// <summary>Rows the run could not write, which is what an operator reads first.</summary>
    public int ItemsFailed { get; set; }
    /// <summary>How the run ended, as a short status word.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>The failure detail when the run did not finish cleanly.</summary>
    public string? ErrorDetails { get; set; }
}
