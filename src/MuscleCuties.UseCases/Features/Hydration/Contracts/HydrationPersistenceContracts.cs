namespace MuscleCuties.UseCases.Features.Hydration.Contracts;

/// <summary>A request to seed the default presets for an account.</summary>
public sealed record InitializeWaterPresetsInputDto(Guid AccountId);
/// <summary>Whether intake is being added or removed.</summary>
public enum WaterIntakeChangeKindDto { Add, Correct, Undo }
/// <summary>A request for one day's intake.</summary>
public sealed record GetWaterIntakeInputDto(Guid AccountId, DateOnly Date);
/// <summary>An intake change, idempotent on its command key.</summary>
public sealed record ChangeWaterIntakeInputDto(Guid AccountId, DateOnly Date, string? ExpectedVersion,
    Guid OperationId, Guid EntryId, WaterIntakeChangeKindDto Kind, int Millilitres);
/// <summary>One intake entry as the client shows it.</summary>
public sealed record WaterIntakeEntryOutputDto(Guid Id, int Millilitres, bool Undone);
/// <summary>One day's intake with its entries and total.</summary>
public sealed record WaterIntakeDayOutputDto(Guid AccountId, DateOnly Date, int LegacyMillilitres,
    int TotalMillilitres, IReadOnlyList<WaterIntakeEntryOutputDto> Entries, string? Version);

// Server-owned configuration has no device permission, notification identifiers or delivery fields.
/// <summary>A request for the reminder settings.</summary>
public sealed record GetWaterReminderConfigurationInputDto(Guid AccountId);
/// <summary>Reminder settings to store.</summary>
public sealed record SaveWaterReminderConfigurationInputDto(Guid AccountId, string? ExpectedVersion,
    bool OptedIn, IReadOnlyList<TimeOnly>? LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd, Guid OperationId);
/// <summary>The stored reminder settings.</summary>
public sealed record WaterReminderConfigurationOutputDto(Guid AccountId, string? Version,
    bool OptedIn, IReadOnlyList<TimeOnly> LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd);
