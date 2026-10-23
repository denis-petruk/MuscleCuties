using MuscleCuties.Core.Domain.Hydration;
using MuscleCuties.UseCases.Features.Hydration.Contracts;

namespace MuscleCuties.UseCases.Features.Hydration;

/// <summary>Validates user intent without persisting settings, requesting permission or scheduling an alert.</summary>
public interface IPreviewWaterReminderUseCase
{
    Task<WaterReminderSettingsOutputDto> ExecuteAsync(PreviewWaterReminderInputDto input, CancellationToken cancellationToken = default);
}

public sealed class PreviewWaterReminderUseCase : IPreviewWaterReminderUseCase
{
    public Task<WaterReminderSettingsOutputDto> ExecuteAsync(PreviewWaterReminderInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var permission = input.Permission switch
        {
            WaterReminderPermissionDto.Granted => WaterReminderAuthorizationState.Granted,
            WaterReminderPermissionDto.Denied => WaterReminderAuthorizationState.Denied,
            _ => WaterReminderAuthorizationState.Unknown
        };
        var settings = input.OptedIn
            ? FixedWaterReminderSettings.Enable(input.LocalTimes, input.QuietStart, input.QuietEnd)
            : FixedWaterReminderSettings.Disabled();
        var output = new WaterReminderSettingsOutputDto(settings.Enabled, settings.LocalTimes, settings.QuietStart,
            settings.QuietEnd, permission switch
            {
                WaterReminderAuthorizationState.Granted => WaterReminderPermissionDto.Granted,
                WaterReminderAuthorizationState.Denied => WaterReminderPermissionDto.Denied,
                _ => WaterReminderPermissionDto.Unknown
            }, settings.CanRequestScheduling(permission));
        return Task.FromResult(output);
    }
}
