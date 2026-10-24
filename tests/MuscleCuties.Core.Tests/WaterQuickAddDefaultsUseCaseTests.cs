using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WaterQuickAddDefaultsUseCaseTests
{
    [Fact]
    public void QuickAddVolumesAreMappedFromTheCorePresetRule()
    {
        var result = new GetWaterQuickAddDefaultsUseCase().Execute();

        Assert.Equal(
            new[]
            {
                DefaultWaterPresets.SmallMillilitres,
                DefaultWaterPresets.MediumMillilitres,
                DefaultWaterPresets.LargeMillilitres
            },
            result.VolumesMillilitres);
    }
}
