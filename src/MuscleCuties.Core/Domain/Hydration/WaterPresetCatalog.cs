namespace MuscleCuties.Core.Domain.Hydration;

/// <summary>Takes a preset's label and millilitres, which the catalogue keeps in order.</summary>
public sealed class WaterPreset
{
    public const int MaximumLabelLength = 80;

    /// <summary>Takes a preset's label and millilitres, which the catalogue keeps in order.</summary>
    private WaterPreset(Guid id, string label, WaterPortion portion)
    {
        Id = id;
        Label = label;
        Portion = portion;
    }

    /// <summary>Identity of the preset.</summary>
    public Guid Id { get; }
    /// <summary>Name the user sees for the preset.</summary>
    public string Label { get; }
    /// <summary>Volume one tap of the preset logs.</summary>
    public WaterPortion Portion { get; }

    /// <summary>Creates a preset, refusing a blank label or a non-positive amount.</summary>
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

    /// <summary>Takes the catalogue's presets, which are always kept in their stored order.</summary>
    private WaterPresetCatalog(Guid accountId, WaterPreset[] presets)
    {
        AccountId = accountId;
        Presets = Array.AsReadOnly(presets);
    }

    /// <summary>Account that owns the catalog.</summary>
    public Guid AccountId { get; }
    /// <summary>The account's presets in display order.</summary>
    public IReadOnlyList<WaterPreset> Presets { get; }

    /// <summary>Rebuilds a catalogue from storage, dropping rows that fail validation rather than failing the read.</summary>
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

    /// <summary>Appends a preset and returns the new catalogue.</summary>
    public WaterPresetCatalog Add(WaterPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return Restore(AccountId, Presets.Append(preset));
    }

    /// <summary>Changes one preset, refusing an id that is not in the catalogue.</summary>
    public WaterPresetCatalog Update(Guid id, string label, double millilitres)
    {
        RequireExisting(id);
        var updated = WaterPreset.Create(id, label, millilitres);
        return Restore(AccountId, Presets.Select(p => p.Id == id ? updated : p));
    }

    /// <summary>Removes the preset and closes the gap in the order.</summary>
    public WaterPresetCatalog Delete(Guid id)
    {
        RequireExisting(id);
        return Restore(AccountId, Presets.Where(p => p.Id != id));
    }

    /// <summary>Stores the order she chose, refusing one that does not name exactly her presets.</summary>
    public WaterPresetCatalog Reorder(IReadOnlyList<Guid> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        if (orderedIds.Count != Presets.Count || orderedIds.Distinct().Count() != orderedIds.Count ||
            orderedIds.Any(id => !Presets.Any(p => p.Id == id)))
            throw new ArgumentException("Reorder must contain every preset identity exactly once.", nameof(orderedIds));
        var byId = Presets.ToDictionary(p => p.Id);
        return Restore(AccountId, orderedIds.Select(id => byId[id]));
    }

    /// <summary>Looks a preset up and refuses an id outside this catalogue.</summary>
    private void RequireExisting(Guid id)
    {
        if (!Presets.Any(p => p.Id == id)) throw new KeyNotFoundException("Preset does not exist in this account.");
    }
}
