namespace MuscleCuties.Core.Domain.Hydration;

/// <summary>Whether water is added or removed, which decides the sign applied.</summary>
public enum WaterIntakeChangeKind { Add, Correct, Undo }
/// <summary>One intake change, idempotent on its command key.</summary>
public sealed record WaterIntakeChange(Guid OperationId, Guid EntryId, WaterIntakeChangeKind Kind, int Millilitres);
/// <summary>One intake entry, with the amount and when it was logged.</summary>
public sealed record WaterIntakeEntry(Guid Id, int Millilitres, bool Undone);

/// <summary>Immutable account/day ledger. Legacy totals remain a separate, untouched baseline.</summary>
public sealed class WaterIntakeDay
{
    // Storage/workload limits, not health targets. Total is limited only by the existing int DTO representation.
    public const int MaximumEntries = 1024;
    public const int MaximumOperations = 4096;

    /// <summary>Rebuilds a day's water log from its entries and recorded operations.</summary>
    private WaterIntakeDay(Guid accountId, DateOnly date, int legacyMillilitres,
        WaterIntakeEntry[] entries, WaterIntakeChange[] operations)
    {
        AccountId = accountId;
        Date = date;
        LegacyMillilitres = legacyMillilitres;
        Entries = Array.AsReadOnly(entries);
        Operations = Array.AsReadOnly(operations);
        TotalMillilitres = checked((int)(legacyMillilitres + entries.Where(e => !e.Undone).Sum(e => (long)e.Millilitres)));
    }

    /// <summary>Account that owns the day.</summary>
    public Guid AccountId { get; }
    /// <summary>Local day the log belongs to.</summary>
    public DateOnly Date { get; }
    /// <summary>Millilitres recorded before portions were tracked, kept for migration.</summary>
    public int LegacyMillilitres { get; }
    /// <summary>Millilitres drunk today, the legacy amount included.</summary>
    public int TotalMillilitres { get; }
    /// <summary>Portion entries logged for the day.</summary>
    public IReadOnlyList<WaterIntakeEntry> Entries { get; }
    /// <summary>Durable operations applied to the day, in order.</summary>
    public IReadOnlyList<WaterIntakeChange> Operations { get; }

    /// <summary>Rehydrates a day from storage after checking the account and a non-negative total.</summary>
    public static WaterIntakeDay Restore(Guid accountId, DateOnly date, int legacyMillilitres,
        IEnumerable<WaterIntakeEntry> entries, IEnumerable<WaterIntakeChange> operations)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(accountId));
        ArgumentOutOfRangeException.ThrowIfNegative(legacyMillilitres);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(operations);
        var items = entries.Take(MaximumEntries + 1).ToArray();
        var receipts = operations.Take(MaximumOperations + 1).ToArray();
        if (items.Length > MaximumEntries || receipts.Length > MaximumOperations || items.Any(e => e is null || e.Id == Guid.Empty) ||
            items.Select(e => e.Id).Distinct().Count() != items.Length || receipts.Any(o => o is null) ||
            receipts.Select(o => o.OperationId).Distinct().Count() != receipts.Length)
            throw new ArgumentException("Intake identities must be unique and within storage limits.");
        foreach (var item in items) WaterPortion.FromMillilitres(item.Millilitres);
        foreach (var receipt in receipts) ValidateChange(receipt);
        return new WaterIntakeDay(accountId, date, legacyMillilitres, items, receipts);
    }

    /// <summary>Applies a change and returns the new day, refusing one that would take the total below zero.</summary>
    public WaterIntakeDay Apply(WaterIntakeChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ValidateChange(change);
        var previous = Operations.SingleOrDefault(o => o.OperationId == change.OperationId);
        if (previous is not null)
        {
            if (previous != change) throw new InvalidOperationException("Operation identity was reused for a different change.");
            return this;
        }
        var existing = Entries.SingleOrDefault(e => e.Id == change.EntryId);
        WaterIntakeEntry[] updated;
        switch (change.Kind)
        {
            case WaterIntakeChangeKind.Add:
                if (existing is not null) throw new InvalidOperationException("Intake entry already exists.");
                updated = [.. Entries, new(change.EntryId, change.Millilitres, false)];
                break;
            case WaterIntakeChangeKind.Correct:
            case WaterIntakeChangeKind.Undo:
                if (existing is null || existing.Undone) throw new InvalidOperationException("An active intake entry is required.");
                updated = Entries.Select(e => e.Id != change.EntryId ? e : change.Kind == WaterIntakeChangeKind.Undo
                    ? e with { Undone = true } : e with { Millilitres = change.Millilitres }).ToArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
        return Restore(AccountId, Date, LegacyMillilitres, updated, Operations.Append(change));
    }

    /// <summary>Refuses an empty command key or a non-positive amount, which would make the day unreplayable.</summary>
    private static void ValidateChange(WaterIntakeChange change)
    {
        if (change.OperationId == Guid.Empty || change.EntryId == Guid.Empty)
            throw new ArgumentException("Stable operation and entry identities are required.", nameof(change));
        if (change.Kind == WaterIntakeChangeKind.Undo)
        {
            if (change.Millilitres != 0) throw new ArgumentException("Undo has no replacement volume.", nameof(change));
        }
        else if (change.Kind is WaterIntakeChangeKind.Add or WaterIntakeChangeKind.Correct)
            WaterPortion.FromMillilitres(change.Millilitres);
        else throw new ArgumentOutOfRangeException(nameof(change));
    }
}
