using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

/// <summary>The quick-add amounts the client offers.</summary>
public interface IGetWaterQuickAddDefaultsUseCase
{
    WaterQuickAddDefaultsDto Execute();
}

/// <summary>Derives the quick adds from her presets, so the buttons match her own measures.</summary>
public sealed class GetWaterQuickAddDefaultsUseCase : IGetWaterQuickAddDefaultsUseCase
{
    /// <summary>The quick-add amounts the water screen offers.</summary>
    public WaterQuickAddDefaultsDto Execute() => new(Array.AsReadOnly<int>(
    [
        DefaultWaterPresets.SmallMillilitres,
        DefaultWaterPresets.MediumMillilitres,
        DefaultWaterPresets.LargeMillilitres
    ]));
}
