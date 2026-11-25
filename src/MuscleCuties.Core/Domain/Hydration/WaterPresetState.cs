namespace MuscleCuties.Core.Domain.Hydration;

/// <summary>Which preset change was made, so the state can be replayed from its operations.</summary>
public enum WaterPresetMutationKind { Create, Update, Delete, Reorder }

/// <summary>Canonical immutable command payload retained with the committed catalog for safe retry.</summary>
public sealed class WaterPresetMutation
{
    /// <summary>Checks and records one durable preset mutation.</summary>
    private WaterPresetMutation(Guid operationId, WaterPresetMutationKind kind, Guid presetId,
        string? label, int millilitres, Guid[] orderedIds)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("A durable operation identity is required.", nameof(operationId));
        OperationId = operationId;
        Kind = kind;
        PresetId = presetId;
        Label = label;
        Millilitres = millilitres;
        OrderedIds = Array.AsReadOnly(orderedIds);
    }

    /// <summary>Identity that makes the mutation idempotent.</summary>
    public Guid OperationId { get; }
    /// <summary>Which preset change the mutation performs.</summary>
    public WaterPresetMutationKind Kind { get; }
    /// <summary>Preset the mutation targets.</summary>
    public Guid PresetId { get; }
    /// <summary>New label for a create or rename.</summary>
    public string? Label { get; }
    /// <summary>Volume in millilitres for a create or resize.</summary>
    public int Millilitres { get; }
    /// <summary>Order the presets take after the mutation, when it reorders them.</summary>
    public IReadOnlyList<Guid> OrderedIds { get; }

    /// <summary>Builds the mutation that creates a preset.</summary>
    public static WaterPresetMutation Create(Guid operationId, string label, double millilitres) =>
        Edit(operationId, WaterPresetMutationKind.Create, operationId, label, millilitres);
    /// <summary>Builds the mutation that renames or resizes an existing preset.</summary>
    public static WaterPresetMutation Update(Guid operationId, Guid presetId, string label, double millilitres) =>
        Edit(operationId, WaterPresetMutationKind.Update, presetId, label, millilitres);
    /// <summary>A deletion of one preset, stored so another device applies the same change.</summary>
    public static WaterPresetMutation Delete(Guid operationId, Guid presetId)
    {
        if (presetId == Guid.Empty) throw new ArgumentException("A preset identity is required.", nameof(presetId));
        return new(operationId, WaterPresetMutationKind.Delete, presetId, null, 0, []);
    }
    /// <summary>A reordering of the whole catalogue.</summary>
    public static WaterPresetMutation Reorder(Guid operationId, IReadOnlyList<Guid> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        if (orderedIds.Count > WaterPresetCatalog.MaximumPresetCount || orderedIds.Any(id => id == Guid.Empty) ||
            orderedIds.Distinct().Count() != orderedIds.Count)
            throw new ArgumentException("Ordered identities must be unique and bounded.", nameof(orderedIds));
        return new(operationId, WaterPresetMutationKind.Reorder, Guid.Empty, null, 0, orderedIds.ToArray());
    }

    /// <summary>True when another mutation carries the same identity and payload, so a retry is not applied twice.</summary>
    public bool Matches(WaterPresetMutation other) => OperationId == other.OperationId && Kind == other.Kind &&
        PresetId == other.PresetId && Label == other.Label && Millilitres == other.Millilitres && OrderedIds.SequenceEqual(other.OrderedIds);

    /// <summary>An edit of one preset's label or amount.</summary>
    private static WaterPresetMutation Edit(Guid operationId, WaterPresetMutationKind kind, Guid presetId, string label, double millilitres)
    {
        var validated = WaterPreset.Create(presetId, label, millilitres);
        return new(operationId, kind, presetId, validated.Label, validated.Portion.Millilitres, []);
    }
}

/// <summary>Takes the catalogue and the operations that produced it.</summary>
public sealed class WaterPresetState
{
    // Bounded durable retry history. Exhaustion rejects a new edit; receipts must never be silently evicted.
    public const int MaximumOperations = 4096;

    /// <summary>Takes the catalogue and the operations that produced it.</summary>
    private WaterPresetState(WaterPresetCatalog catalog, WaterPresetMutation[] operations)
    {
        Catalog = catalog;
        Operations = Array.AsReadOnly(operations);
    }

    /// <summary>Catalog state after the applied mutations.</summary>
    public WaterPresetCatalog Catalog { get; }
    /// <summary>Durable mutations applied to the catalog, in order.</summary>
    public IReadOnlyList<WaterPresetMutation> Operations { get; }

    /// <summary>Rebuilds the state by replaying its operations, so a device converges on the same catalogue.</summary>
    public static WaterPresetState Restore(WaterPresetCatalog catalog, IEnumerable<WaterPresetMutation> operations)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(operations);
        var receipts = operations.Take(MaximumOperations + 1).ToArray();
        if (receipts.Length > MaximumOperations || receipts.Any(o => o is null) ||
            receipts.Select(o => o.OperationId).Distinct().Count() != receipts.Length)
            throw new ArgumentException("Preset operation receipts must be unique and bounded.", nameof(operations));
        return new(catalog, receipts);
    }

    /// <summary>Applies one operation, refusing the whole batch when any of them fails validation.</summary>
    public WaterPresetState Apply(WaterPresetMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var previous = Operations.SingleOrDefault(o => o.OperationId == mutation.OperationId);
        if (previous is not null)
        {
            if (!previous.Matches(mutation)) throw new InvalidOperationException("Operation identity was reused for a different preset change.");
            return this;
        }
        var updated = mutation.Kind switch
        {
            WaterPresetMutationKind.Create => Catalog.Add(WaterPreset.Create(mutation.PresetId, mutation.Label!, mutation.Millilitres)),
            WaterPresetMutationKind.Update => Catalog.Update(mutation.PresetId, mutation.Label!, mutation.Millilitres),
            WaterPresetMutationKind.Delete => Catalog.Delete(mutation.PresetId),
            WaterPresetMutationKind.Reorder => Catalog.Reorder(mutation.OrderedIds),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        return Restore(updated, Operations.Append(mutation));
    }
}
