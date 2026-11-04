namespace MuscleCuties.UseCases.Features.Hydration.Contracts;

public sealed record WaterPresetOutputDto(Guid Id, string Label, int Millilitres);
public sealed record WaterQuickAddDefaultsDto(IReadOnlyList<int> VolumesMillilitres);
public sealed record WaterPresetCatalogOutputDto(Guid AccountId, string? Version, IReadOnlyList<WaterPresetOutputDto> Presets);
public sealed record GetWaterPresetsInputDto(Guid AccountId);
public sealed record CreateWaterPresetInputDto(Guid AccountId, string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
public sealed record UpdateWaterPresetInputDto(Guid AccountId, Guid PresetId, string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
public sealed record DeleteWaterPresetInputDto(Guid AccountId, Guid PresetId, Guid OperationId, string? ExpectedVersion);
public sealed record ReorderWaterPresetsInputDto(Guid AccountId, IReadOnlyList<Guid> OrderedIds, Guid OperationId, string? ExpectedVersion);

public enum WaterReminderPermissionDto { Unknown, Denied, Granted }
public sealed record PreviewWaterReminderInputDto(
    bool OptedIn, IReadOnlyList<TimeOnly>? LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd,
    WaterReminderPermissionDto Permission);
public sealed record WaterReminderSettingsOutputDto(
    bool OptedIn, IReadOnlyList<TimeOnly> LocalTimes, TimeOnly? QuietStart, TimeOnly? QuietEnd,
    WaterReminderPermissionDto Permission, bool CanRequestScheduling);
