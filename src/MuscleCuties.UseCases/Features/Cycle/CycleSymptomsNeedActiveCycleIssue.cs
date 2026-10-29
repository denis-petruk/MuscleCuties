namespace MuscleCuties.UseCases.Features.Cycle;

/// <summary>
/// Raised when a daily log carries symptoms but the logged date has no active cycle to attach them to.
/// This is a distinct type, not a generic failure, because the client can recover from it: recording a
/// period start creates the cycle. Derives from <see cref="InvalidOperationException"/> so callers that
/// guard on the base type keep their current behaviour.
/// </summary>
public sealed class CycleSymptomsNeedActiveCycleIssue(string message) : InvalidOperationException(message);
