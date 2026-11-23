namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// The experience tier consumed by the live progression and volume rules. The resolver rule that computed it
/// (`EffectiveExperienceTierRule`) had no production caller and was removed with its tests.
/// </summary>
public enum EffectiveExperienceTier
{
    Tier1BeginnerOrNew = 1,
    Tier2IntermediateOrEstablished = 2,
    Tier3Advanced = 3
}
