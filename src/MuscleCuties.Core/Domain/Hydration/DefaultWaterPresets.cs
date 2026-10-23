namespace MuscleCuties.Core.Domain.Hydration;

public static class DefaultWaterPresets
{
    public const int SmallMillilitres = 250;
    public const int MediumMillilitres = 500;
    public const int LargeMillilitres = 750;
    private const string SmallLabel = "250 ml";
    private const string MediumLabel = "500 ml";
    private const string LargeLabel = "750 ml";

    /// <summary>Owner-approved editable starting entries; never invoke to refill a deleted catalog.</summary>
    public static WaterPresetCatalog Create(Guid accountId) => WaterPresetCatalog.Restore(accountId,
    [
        WaterPreset.Create(Guid.NewGuid(), SmallLabel, SmallMillilitres),
        WaterPreset.Create(Guid.NewGuid(), MediumLabel, MediumMillilitres),
        WaterPreset.Create(Guid.NewGuid(), LargeLabel, LargeMillilitres)
    ]);
}
