using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class TemplateVolumeFloorRuleTests
{
    private const string UnknownTemplateId = "MC-UNKNOWN-TEMPLATE-V0";
    private const int BelowFloorWorkingSets = 3;
    private const int ValidReducedFullBodyWorkingSets = 6;
    private const int ValidReducedUpperLowerWorkingSets = 4;
    private const int ZeroSets = 0;

    [Fact]
    public void AllCanonicalTemplates_HaveClinicallyReviewedVolumeFloors()
    {
        var allTemplates = StrengthTemplateCatalog.Templates;
        Assert.NotEmpty(allTemplates);

        foreach (var template in allTemplates)
        {
            var floor = TemplateVolumeFloorRule.GetReviewedFloor(template.TemplateId);
            Assert.NotNull(floor);
            Assert.Equal(template.TemplateId, floor.TemplateId);
            Assert.Equal(TemplateVolumeFloorRule.ReviewedStatus, floor.ReviewStatus);
            Assert.True(floor.MinimumWorkingSets > ZeroSets);
            Assert.True(floor.MinimumExercises > ZeroSets);

            foreach (var slot in template.Slots)
            {
                Assert.True(floor.MinimumSetsBySlotId.ContainsKey(slot.SlotId));
                var minSets = floor.MinimumSetsBySlotId[slot.SlotId];
                if (slot.IsRequired)
                {
                    Assert.True(minSets > ZeroSets,
                        $"Required slot '{slot.SlotId}' in template '{template.TemplateId}' must have a positive minimum set floor.");
                }
                else
                {
                    Assert.Equal(TemplateVolumeFloorRule.OptionalSlotMinimumSets, minSets);
                }
            }
        }
    }

    [Fact]
    public void TemplateFloorRequiresExplicitReviewStatus_WhenCustomSupplied()
    {
        var template = StrengthTemplateCatalog.ForSchedule(2)[0];
        var unreviewedFloor = new TemplateVolumeFloor(template.TemplateId, 2, 1,
            new Dictionary<string, int> { [template.Slots[0].SlotId] = 1 },
            ReviewStatus: TemplateVolumeFloorRule.UnreviewedStatus);
        var supplied = new Dictionary<string, TemplateVolumeFloor> { [template.TemplateId] = unreviewedFloor };

        // Standard catalog lookup returns clinically reviewed floor
        Assert.NotNull(TemplateVolumeFloorRule.GetReviewedFloor(template.TemplateId));

        // Explicit unreviewed custom override fails closed
        Assert.Null(TemplateVolumeFloorRule.GetReviewedFloor(template.TemplateId, supplied));
    }

    [Fact]
    public void UnrecognizedTemplate_FailsClosedWithMissingReview()
    {
        var floor = TemplateVolumeFloorRule.GetReviewedFloor(UnknownTemplateId);
        Assert.Null(floor);

        var eval = TemplateVolumeFloorRule.Evaluate(UnknownTemplateId, ValidReducedFullBodyWorkingSets);
        Assert.False(eval.IsAllowed);
        Assert.Equal(TemplateVolumeFloorRule.TemplateVolumeFloorReviewMissing, eval.LimitationCode);
    }

    [Fact]
    public void VolumeBelowSessionFloor_FailsWithVolumeBelowTemplateFloor()
    {
        var eval = TemplateVolumeFloorRule.Evaluate(
            TemplateVolumeFloorRule.FullBody1DayTemplateId,
            BelowFloorWorkingSets);

        Assert.False(eval.IsAllowed);
        Assert.Equal(TemplateVolumeFloorRule.VolumeBelowTemplateFloor, eval.LimitationCode);
    }

    [Fact]
    public void SlotVolumeBelowFloor_FailsWithVolumeBelowTemplateFloor()
    {
        var slotSets = new Dictionary<string, int>
        {
            [TemplateVolumeFloorRule.KneeDominantSlotId] = ZeroSets,
            [TemplateVolumeFloorRule.HipDominantSlotId] = 2,
            [TemplateVolumeFloorRule.UpperPushSlotId] = 2,
            [TemplateVolumeFloorRule.UpperPullSlotId] = 2
        };

        var eval = TemplateVolumeFloorRule.Evaluate(
            TemplateVolumeFloorRule.FullBody1DayTemplateId,
            ValidReducedFullBodyWorkingSets,
            actualSetsBySlot: slotSets);

        Assert.False(eval.IsAllowed);
        Assert.Equal(TemplateVolumeFloorRule.VolumeBelowTemplateFloor, eval.LimitationCode);
    }

    [Fact]
    public void ValidReducedVolume_IsAllowed()
    {
        var eval = TemplateVolumeFloorRule.Evaluate(
            TemplateVolumeFloorRule.FullBody1DayTemplateId,
            ValidReducedFullBodyWorkingSets);

        Assert.True(eval.IsAllowed);
        Assert.Null(eval.LimitationCode);
    }

    [Fact]
    public void UpperLowerReducedVolume_IsAllowedAtFloor()
    {
        var eval = TemplateVolumeFloorRule.Evaluate(
            TemplateVolumeFloorRule.UpperLower4DayUpper1TemplateId,
            ValidReducedUpperLowerWorkingSets);

        Assert.True(eval.IsAllowed);
        Assert.Null(eval.LimitationCode);
    }
}
