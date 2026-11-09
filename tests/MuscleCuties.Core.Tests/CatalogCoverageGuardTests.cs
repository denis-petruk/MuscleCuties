using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// D1 evidence: every required reviewed template slot has eligible candidates for each equipment
/// profile offered in Beta and each experience tier. Real gaps are listed explicitly, never faked.
/// </summary>
public sealed class CatalogCoverageGuardTests
{
    private const int MinimumCandidates = 1;
    private const int RecommendedCandidates = 2;

    // Bodyweight only is being withdrawn from new Beta selection (OD-2); it has no squat, lunge,
    // vertical push or upper-pull candidate in the reviewed catalog and is deliberately not guarded here.
    private static readonly Equipment[] OfferedProfiles = [Equipment.FullGym, Equipment.HomeDumbbellsBands];

    private static readonly ActivityExperience[] Tiers =
        [ActivityExperience.Beginner, ActivityExperience.Returning, ActivityExperience.Established];

    // Known gap (audit D1): with dumbbells and bands the only reviewed upper-pull candidate is the
    // Single-Arm DB Row (145). There is no vertical pull and no band row in the catalog, so these
    // slots have one candidate instead of the recommended two until a catalog review adds movements.
    private static readonly HashSet<string> KnownSingleCandidateSlots = new(StringComparer.Ordinal)
    {
        "HomeDumbbellsBands|MC-FB-1D-A|upper-pull",
        "HomeDumbbellsBands|MC-FB-2D-A|upper-pull",
        "HomeDumbbellsBands|MC-FB-2D-B|upper-pull",
        "HomeDumbbellsBands|MC-FB-3D-A|upper-pull",
        "HomeDumbbellsBands|MC-FB-3D-B|upper-pull",
        "HomeDumbbellsBands|MC-FB-3D-C|upper-pull",
        "HomeDumbbellsBands|MC-UL-4D-UPPER-A|upper-pull",
        "HomeDumbbellsBands|MC-UL-4D-UPPER-B|upper-pull"
    };

    [Fact]
    public void Every_required_slot_has_an_eligible_candidate_for_offered_profiles_and_tiers()
    {
        var thin = new HashSet<string>(StringComparer.Ordinal);
        foreach (var equipment in OfferedProfiles)
        foreach (var experience in Tiers)
        foreach (var (templateId, slotId, ids) in RequiredSlots())
        {
            var count = EligibleCount(ids, equipment, experience);
            Assert.True(count >= MinimumCandidates,
                $"{equipment}/{experience}: {templateId} {slotId} has no eligible reviewed candidate.");
            if (count < RecommendedCandidates)
                thin.Add($"{equipment}|{templateId}|{slotId}");
        }

        Assert.Equal(KnownSingleCandidateSlots.OrderBy(key => key, StringComparer.Ordinal),
            thin.OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void Bodyweight_only_gap_is_real_and_documented()
    {
        var empty = RequiredSlots().Where(slot =>
            EligibleCount(slot.Ids, Equipment.Bodyweight, ActivityExperience.Beginner) == 0).ToArray();

        Assert.Contains(empty, slot => slot.SlotId == "knee-dominant");
        Assert.Contains(empty, slot => slot.SlotId == "upper-pull");
    }

    private static int EligibleCount(IReadOnlyCollection<int> ids, Equipment equipment, ActivityExperience experience)
    {
        var candidates = ids.Select(id => ReviewedCatalogFixture.Entry(id).Candidate).ToArray();
        var context = new ExerciseEligibilityContext(StrengthPlanPreviewRule.AvailableEquipmentFor(equipment), experience,
            new HashSet<int>(), new HashSet<MovementPattern>(), new HashSet<ActivityDemandTag>(), false);
        return ExerciseEligibilityRule.Filter(candidates, context).EligibleCandidates
            .Count(candidate => candidate.SupportedTimingMethods?.Contains(ExerciseTimingMethod.StraightSets) == true);
    }

    private static IEnumerable<(string TemplateId, string SlotId, int[] Ids)> RequiredSlots() =>
        ReviewedCatalogFixture.Root.GetProperty("templateSlots").EnumerateArray()
            .Where(slot => slot.GetProperty("requiredOrOptional").GetString() == "Required")
            .Select(slot => (
                slot.GetProperty("templateIdAndVersion").GetString()!.Split(" / ")[0],
                slot.GetProperty("movementSlot").GetString()!,
                slot.GetProperty("candidateSourceExerciseIds").EnumerateArray().Select(value => value.GetInt32()).ToArray()));
}
