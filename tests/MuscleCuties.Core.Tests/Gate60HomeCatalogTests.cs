using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.Catalog;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class Gate60HomeCatalogTests
{
    private static readonly (int Id, string Name, MovementPattern Pattern, int RequiredFlags, bool UnilateralDoublesTime)[] ApprovedGate60Exercises =
    [
        (108, "Floor Glute Bridge", MovementPattern.HipThrust, 0, false),
        (119, "Slider Leg Curl", MovementPattern.KneeFlexion, 1024, false),
        (129, "Bodyweight Squat", MovementPattern.SquatPattern, 0, false),
        (130, "Bodyweight Reverse Lunge", MovementPattern.Lunge, 0, true),
        (138, "Bodyweight Split Squat", MovementPattern.Lunge, 0, true),
        (148, "Resistance Band Row", MovementPattern.HorizontalPull, 16 | 2048, false),
        (159, "Resistance Band Chest Press", MovementPattern.HorizontalPush, 16 | 2048, false),
        (165, "Resistance Band Biceps Curl", MovementPattern.ElbowFlexion, 16, false)
    ];

    [Fact]
    public void Equipment_enums_define_explicit_sliders_and_band_anchor_capabilities()
    {
        Assert.True(Enum.TryParse<EquipmentSet>("Sliders", out var sliderSet));
        Assert.Equal(1024, (int)sliderSet);
        Assert.True(Enum.TryParse<EquipmentSet>("BandAnchor", out var anchorSet));
        Assert.Equal(2048, (int)anchorSet);

        Assert.True(Enum.TryParse<ExerciseEquipmentKind>("Sliders", out _));
        Assert.True(Enum.TryParse<ExerciseEquipmentKind>("BandAnchor", out _));
    }

    [Fact]
    public void Gate60_all_eight_approved_exercises_exist_with_reviewed_metadata()
    {
        foreach (var expected in ApprovedGate60Exercises)
        {
            Assert.True(CatalogAuditFixture.Definitions.TryGetValue(expected.Id, out var definition),
                $"Missing WorkoutExerciseDefinition {expected.Id} ({expected.Name}).");
            Assert.Equal(expected.Name, definition!.Name);
            Assert.Equal((int)expected.Pattern, definition.Pattern);
            Assert.Equal(expected.RequiredFlags, definition.Equipment);
            Assert.Equal(expected.UnilateralDoublesTime, definition.UnilateralDoublesTime);

            Assert.True(CatalogAuditFixture.Reviews.ContainsKey(expected.Id),
                $"Missing reviewed catalog record for {expected.Id} ({expected.Name}).");
            Assert.True(CatalogAuditFixture.Contributions.TryGetValue(expected.Id, out var contribs) && contribs.Count > 0,
                $"Missing muscle contributions for {expected.Id} ({expected.Name}).");

        }
    }

    [Fact]
    public void Gate60_existing_dumbbell_variations_and_dynamic_core_entries_remain_intact()
    {
        // ID 128 Dumbbell Reverse Lunge and ID 125 Bulgarian Split Squat keep their identity and dumbbell requirement.
        Assert.Equal("Reverse Lunge", CatalogAuditFixture.Definitions[128].Name);
        Assert.Equal((int)EquipmentSet.Dumbbell, CatalogAuditFixture.Definitions[128].Equipment);
        Assert.Equal("Bulgarian Split Squat", CatalogAuditFixture.Definitions[125].Name);
        Assert.Equal((int)EquipmentSet.Dumbbell, CatalogAuditFixture.Definitions[125].Equipment);

    }

    [Fact]
    public void Gate60_slider_and_anchor_exercises_require_explicit_capability_confirmation()
    {
        var bodyweightOnly = new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight };
        var bandsWithoutAnchor = new HashSet<ExerciseEquipmentKind>
            { ExerciseEquipmentKind.Bodyweight, ExerciseEquipmentKind.ResistanceBand };

        var sliderCurl = ReviewedCatalogFixture.Entry(119).Candidate;
        var bandRow = ReviewedCatalogFixture.Entry(148).Candidate;
        var bandPress = ReviewedCatalogFixture.Entry(159).Candidate;
        var bandCurl = ReviewedCatalogFixture.Entry(165).Candidate;

        Assert.False(ExerciseEligibilityRule.Filter([sliderCurl],
            new ExerciseEligibilityContext(bodyweightOnly, ActivityExperience.Beginner,
                new HashSet<int>(), new HashSet<MovementPattern>(), new HashSet<ActivityDemandTag>(), false))
            .EligibleCandidates.Any());

        var noAnchorResult = ExerciseEligibilityRule.Filter([bandRow, bandPress, bandCurl],
            new ExerciseEligibilityContext(bandsWithoutAnchor, ActivityExperience.Beginner,
                new HashSet<int>(), new HashSet<MovementPattern>(), new HashSet<ActivityDemandTag>(), false));

        var eligibleIds = noAnchorResult.EligibleCandidates.Select(c => c.ExerciseId).ToArray();
        Assert.Equal([165], eligibleIds);
    }
}
