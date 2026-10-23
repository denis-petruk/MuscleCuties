using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

public sealed class InitializeWaterPresetsUseCase(IWaterPresetInitializationStore store)
{
    public async Task<WaterPresetCatalogOutputDto> ExecuteAsync(InitializeWaterPresetsInputDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();
        var defaults = DefaultWaterPresets.Create(input.AccountId);
        await store.TryInitializeAsync(defaults, cancellationToken).ConfigureAwait(false);
        // Always project the acknowledged state, including concurrent initialization or deliberate deletion.
        var stored = await WaterPresetOperation.ReadAsync(store, input.AccountId, cancellationToken).ConfigureAwait(false);
        return WaterPresetOperation.Map(stored);
    }
}
