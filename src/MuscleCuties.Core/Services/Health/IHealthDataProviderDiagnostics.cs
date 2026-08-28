namespace MuscleCuties.Core.Services.Health;

public interface IHealthDataProviderDiagnostics
{
    string UnavailableMessage { get; }
    string EmptyDataMessage { get; }
}
