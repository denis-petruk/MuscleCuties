namespace MuscleCuties.Core.Domain.Hydration;

public sealed class WaterPreset
{
    public const int MaximumLabelLength = 80;

    private WaterPreset(Guid id, string label, WaterPortion portion)
    {
        Id = id;
        Label = label;
        Portion = portion;
    }

    public Guid Id { get; }
    public string Label { get; }
    public WaterPortion Portion { get; }

    public static WaterPreset Create(Guid id, string label, double millilitres)
    {
        if (id == Guid.Empty) throw new ArgumentException("A stable preset identity is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > MaximumLabelLength)
            throw new ArgumentException("Enter a label within the supported length.", nameof(label));
        return new WaterPreset(id, label.Trim(), WaterPortion.FromMillilitres(millilitres));
    }
}

/// <summary>Immutable ordered catalog; edits preserve identity and never alter logged intake.</summary>
public sealed class WaterPresetCatalog
{
    // Bounded UI list and atomic persistence payload; no built-in volumes are selected here.
    public const int MaximumPresetCount = 32;

    private WaterPresetCatalog(Guid accountId, WaterPreset[] presets)
    {
        AccountId = accountId;
        Presets = Array.AsReadOnly(presets);
    }

    public Guid AccountId { get; }
    public IReadOnlyList<WaterPreset> Presets { get; }

    public static WaterPresetCatalog Restore(Guid accountId, IEnumerable<WaterPreset> presets)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(accountId));
        ArgumentNullException.ThrowIfNull(presets);
        var items = presets.Take(MaximumPresetCount + 1).ToArray();
        if (items.Length > MaximumPresetCount || items.Any(p => p is null) ||
            items.Select(p => p.Id).Distinct().Count() != items.Length ||
            items.Select(p => p.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new ArgumentException("Presets must have unique identities and labels within the list limit.", nameof(presets));
        return new WaterPresetCatalog(accountId, items);
    }

    public WaterPresetCatalog Add(WaterPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return Restore(AccountId, Presets.Append(preset));
    }

    public WaterPresetCatalog Update(Guid id, string label, double millilitres)
    {
        RequireExisting(id);
        var updated = WaterPreset.Create(id, label, millilitres);
        return Restore(AccountId, Presets.Select(p => p.Id == id ? updated : p));
    }

    public WaterPresetCatalog Delete(Guid id)
    {
        RequireExisting(id);
        return Restore(AccountId, Presets.Where(p => p.Id != id));
    }

    public WaterPresetCatalog Reorder(IReadOnlyList<Guid> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        if (orderedIds.Count != Presets.Count || orderedIds.Distinct().Count() != orderedIds.Count ||
            orderedIds.Any(id => !Presets.Any(p => p.Id == id)))
            throw new ArgumentException("Reorder must contain every preset identity exactly once.", nameof(orderedIds));
        var byId = Presets.ToDictionary(p => p.Id);
        return Restore(AccountId, orderedIds.Select(id => byId[id]));
    }

    private void RequireExisting(Guid id)
    {
        if (!Presets.Any(p => p.Id == id)) throw new KeyNotFoundException("Preset does not exist in this account.");
    }
}
