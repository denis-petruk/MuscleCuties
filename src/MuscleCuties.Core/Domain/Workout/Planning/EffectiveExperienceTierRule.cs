namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>The tier planning actually uses, after her experience is capped by what the plan can support.</summary>
public enum EffectiveExperienceTier
{
    Tier1BeginnerOrNew = 1,
    Tier2IntermediateOrEstablished = 2,
    Tier3Advanced = 3
}
