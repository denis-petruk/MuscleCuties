namespace MuscleCuties.Core.Domain.Hydration;

public enum WaterPresetMutationKind { Create, Update, Delete, Reorder }

/// <summary>Canonical immutable command payload retained with the committed catalog for safe retry.</summary>
public sealed class WaterPresetMutation
{
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

    public Guid OperationId { get; }
    public WaterPresetMutationKind Kind { get; }
    public Guid PresetId { get; }
    public string? Label { get; }
    public int Millilitres { get; }
    public IReadOnlyList<Guid> OrderedIds { get; }

    public static WaterPresetMutation Create(Guid operationId, string label, double millilitres) =>
        Edit(operationId, WaterPresetMutationKind.Create, operationId, label, millilitres);
    public static WaterPresetMutation Update(Guid operationId, Guid presetId, string label, double millilitres) =>
        Edit(operationId, WaterPresetMutationKind.Update, presetId, label, millilitres);
    public static WaterPresetMutation Delete(Guid operationId, Guid presetId)
    {
        if (presetId == Guid.Empty) throw new ArgumentException("A preset identity is required.", nameof(presetId));
        return new(operationId, WaterPresetMutationKind.Delete, presetId, null, 0, []);
    }
    public static WaterPresetMutation Reorder(Guid operationId, IReadOnlyList<Guid> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        if (orderedIds.Count > WaterPresetCatalog.MaximumPresetCount || orderedIds.Any(id => id == Guid.Empty) ||
            orderedIds.Distinct().Count() != orderedIds.Count)
            throw new ArgumentException("Ordered identities must be unique and bounded.", nameof(orderedIds));
        return new(operationId, WaterPresetMutationKind.Reorder, Guid.Empty, null, 0, orderedIds.ToArray());
    }

    public bool Matches(WaterPresetMutation other) => OperationId == other.OperationId && Kind == other.Kind &&
        PresetId == other.PresetId && Label == other.Label && Millilitres == other.Millilitres && OrderedIds.SequenceEqual(other.OrderedIds);

    private static WaterPresetMutation Edit(Guid operationId, WaterPresetMutationKind kind, Guid presetId, string label, double millilitres)
    {
        var validated = WaterPreset.Create(presetId, label, millilitres);
        return new(operationId, kind, presetId, validated.Label, validated.Portion.Millilitres, []);
    }
}

public sealed class WaterPresetState
{
    // Bounded durable retry history. Exhaustion rejects a new edit; receipts must never be silently evicted.
    public const int MaximumOperations = 4096;

    private WaterPresetState(WaterPresetCatalog catalog, WaterPresetMutation[] operations)
    {
        Catalog = catalog;
        Operations = Array.AsReadOnly(operations);
    }

    public WaterPresetCatalog Catalog { get; }
    public IReadOnlyList<WaterPresetMutation> Operations { get; }

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
