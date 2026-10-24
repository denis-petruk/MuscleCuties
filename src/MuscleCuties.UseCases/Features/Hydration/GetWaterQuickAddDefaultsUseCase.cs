using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

public interface IGetWaterQuickAddDefaultsUseCase
{
    WaterQuickAddDefaultsDto Execute();
}

public sealed class GetWaterQuickAddDefaultsUseCase : IGetWaterQuickAddDefaultsUseCase
{
    public WaterQuickAddDefaultsDto Execute() => new(Array.AsReadOnly<int>(
    [
        DefaultWaterPresets.SmallMillilitres,
        DefaultWaterPresets.MediumMillilitres,
        DefaultWaterPresets.LargeMillilitres
    ]));
}
