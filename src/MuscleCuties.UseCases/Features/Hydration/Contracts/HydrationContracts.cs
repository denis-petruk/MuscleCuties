namespace MuscleCuties.UseCases.Features.Hydration.Contracts;

/// <summary>One water preset as the client shows it.</summary>
public sealed record WaterPresetOutputDto(Guid Id, string Label, int Millilitres);
/// <summary>The amounts offered as quick adds, derived from her presets.</summary>
public sealed record WaterQuickAddDefaultsDto(IReadOnlyList<int> VolumesMillilitres);
/// <summary>Her presets in order, with the account they belong to.</summary>
public sealed record WaterPresetCatalogOutputDto(Guid AccountId, string? Version, IReadOnlyList<WaterPresetOutputDto> Presets);
/// <summary>A request for the presets of one account.</summary>
public sealed record GetWaterPresetsInputDto(Guid AccountId);
/// <summary>A new preset to add, with its place in the order.</summary>
public sealed record CreateWaterPresetInputDto(Guid AccountId, string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
/// <summary>A preset to change.</summary>
public sealed record UpdateWaterPresetInputDto(Guid AccountId, Guid PresetId, string Label, double Millilitres, Guid OperationId, string? ExpectedVersion);
/// <summary>A preset to remove.</summary>
public sealed record DeleteWaterPresetInputDto(Guid AccountId, Guid PresetId, Guid OperationId, string? ExpectedVersion);
/// <summary>The full order she chose, which replaces the stored sequence.</summary>
public sealed record ReorderWaterPresetsInputDto(Guid AccountId, IReadOnlyList<Guid> OrderedIds, Guid OperationId, string? ExpectedVersion);

