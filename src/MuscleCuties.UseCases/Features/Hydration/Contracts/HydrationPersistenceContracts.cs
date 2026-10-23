namespace MuscleCuties.UseCases.Features.Hydration.Contracts;

public sealed record InitializeWaterPresetsInputDto(Guid AccountId);
public enum WaterIntakeChangeKindDto { Add, Correct, Undo }
public sealed record GetWaterIntakeInputDto(Guid AccountId, DateOnly Date);
public sealed record ChangeWaterIntakeInputDto(Guid AccountId, DateOnly Date, string? ExpectedVersion,
    Guid OperationId, Guid EntryId, WaterIntakeChangeKindDto Kind, int Millilitres);
public sealed record WaterIntakeEntryOutputDto(Guid Id, int Millilitres, bool Undone);
public sealed record WaterIntakeDayOutputDto(Guid AccountId, DateOnly Date, int LegacyMillilitres,
    int TotalMillilitres, IReadOnlyList<WaterIntakeEntryOutputDto> Entries, string? Version);

// Server-owned configuration has no device permission, notification identifiers or delivery fields.
public sealed record GetWaterReminderConfigurationInputDto(Guid AccountId);
public sealed record SaveWaterReminderConfigurationInputDto(Guid AccountId, string? ExpectedVersion,
    bool OptedIn, IReadOnlyList<TimeOnly>? LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd, Guid OperationId);
public sealed record WaterReminderConfigurationOutputDto(Guid AccountId, string? Version,
    bool OptedIn, IReadOnlyList<TimeOnly> LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd);
