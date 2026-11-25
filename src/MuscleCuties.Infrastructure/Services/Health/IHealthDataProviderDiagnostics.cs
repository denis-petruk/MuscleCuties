namespace MuscleCuties.Core.Services.Health;

/// <summary>The diagnostics a provider can report about itself, for the settings page's status line.</summary>
public interface IHealthDataProviderDiagnostics
{
    string UnavailableMessage { get; }
    string EmptyDataMessage { get; }
}
