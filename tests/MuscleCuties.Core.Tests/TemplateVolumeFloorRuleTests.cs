using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class TemplateVolumeFloorRuleTests
{
    private const int SyntheticFloorSets = 6;
    private const int SyntheticFloorExercises = 3;
    private const int SyntheticSlotFloor = 1;
    private const int BelowFloorSets = 3;
    private const int EmptySets = 0;

    [Fact]
    public void AvailableSlotFloor_AllowsOneSetPerSafeRequiredMovementAndOmittedAccessory()
    {
        var templateId = TemplateVolumeFloorRule.FullBody1DayTemplateId;
        var knee = Slot(TemplateVolumeFloorRule.KneeDominantSlotId, SyntheticSlotFloor);
        var push = Slot(TemplateVolumeFloorRule.UpperPushSlotId, SyntheticSlotFloor);
        var unavailableHip = Slot(TemplateVolumeFloorRule.HipDominantSlotId, SyntheticSlotFloor) with { ExerciseId = null };
        var accessory = Slot(TemplateVolumeFloorRule.TrunkSlotId, SyntheticSlotFloor) with { IsRequired = false };
        var floor = TemplateVolumeFloorRule.ForAvailableSlots(templateId, [knee, push, unavailableHip, accessory]);
        var floors = new Dictionary<string, TemplateVolumeFloor> { [templateId] = floor };

        Assert.True(TemplateVolumeFloorRule.EvaluateSelections(templateId, [knee, push], floors).IsAllowed);
        Assert.False(TemplateVolumeFloorRule.EvaluateSelections(templateId, [knee], floors).IsAllowed);
    }

    [Fact]
    public void CanonicalTemplates_WithoutReviewEvidence_FailClosed()
    {
        Assert.NotEmpty(StrengthTemplateCatalog.Templates);
        foreach (var template in StrengthTemplateCatalog.Templates)
        {
            Assert.Null(TemplateVolumeFloorRule.GetReviewedFloor(template.TemplateId));
            var result = TemplateVolumeFloorRule.Evaluate(template.TemplateId, SyntheticFloorSets);
            Assert.False(result.IsAllowed);
            Assert.Equal(TemplateVolumeFloorRule.TemplateVolumeFloorReviewMissing, result.LimitationCode);
            Assert.DoesNotContain("clinical", result.ReasonDetail!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ExplicitUnreviewedFloor_FailsClosed()
    {
        var templateId = TemplateVolumeFloorRule.FullBody1DayTemplateId;
        var floor = new TemplateVolumeFloor(templateId, SyntheticFloorSets, SyntheticFloorExercises,
            new Dictionary<string, int> { [TemplateVolumeFloorRule.KneeDominantSlotId] = SyntheticSlotFloor });
        Assert.Null(TemplateVolumeFloorRule.GetReviewedFloor(templateId,
            new Dictionary<string, TemplateVolumeFloor> { [templateId] = floor }));
    }

    [Fact]
    public void SuppliedReviewedFloor_EnforcesOverallAndSlotMinimums()
    {
        var templateId = TemplateVolumeFloorRule.FullBody1DayTemplateId;
        var floor = new TemplateVolumeFloor(templateId, SyntheticFloorSets, SyntheticFloorExercises,
            new Dictionary<string, int> { [TemplateVolumeFloorRule.KneeDominantSlotId] = SyntheticSlotFloor },
            TemplateVolumeFloorRule.ReviewedStatus);
        var floors = new Dictionary<string, TemplateVolumeFloor> { [templateId] = floor };
        Assert.Equal(TemplateVolumeFloorRule.VolumeBelowTemplateFloor,
            TemplateVolumeFloorRule.Evaluate(templateId, BelowFloorSets, customFloors: floors).LimitationCode);
        Assert.Equal(TemplateVolumeFloorRule.VolumeBelowTemplateFloor,
            TemplateVolumeFloorRule.Evaluate(templateId, SyntheticFloorSets,
                new Dictionary<string, int> { [TemplateVolumeFloorRule.KneeDominantSlotId] = EmptySets }, floors).LimitationCode);
        Assert.True(TemplateVolumeFloorRule.Evaluate(templateId, SyntheticFloorSets,
            new Dictionary<string, int> { [TemplateVolumeFloorRule.KneeDominantSlotId] = SyntheticSlotFloor }, floors).IsAllowed);
    }
    [Fact]
    public void SelectionFloor_RejectsMissingRequiredSlotAndTooFewExercises()
    {
        var floors = SyntheticFloors();
        var templateId = TemplateVolumeFloorRule.FullBody1DayTemplateId;
        var missingRequired = new[] { Slot("other", SyntheticFloorSets) };
        Assert.False(TemplateVolumeFloorRule.EvaluateSelections(templateId, missingRequired, floors).IsAllowed);
        var tooFew = new[] { Slot(TemplateVolumeFloorRule.KneeDominantSlotId, SyntheticFloorSets) };
        Assert.False(TemplateVolumeFloorRule.EvaluateSelections(templateId, tooFew, floors).IsAllowed);
        var sufficient = new[] { Slot(TemplateVolumeFloorRule.KneeDominantSlotId, SyntheticSlotFloor),
            Slot("second", SyntheticSlotFloor), Slot("third", SyntheticFloorSets) };
        Assert.True(TemplateVolumeFloorRule.EvaluateSelections(templateId, sufficient, floors).IsAllowed);
    }

    [Fact]
    public void SelectionFloor_RejectsDuplicateOrBlankSlotsWithoutThrowing()
    {
        var floors = SyntheticFloors();
        var templateId = TemplateVolumeFloorRule.FullBody1DayTemplateId;
        Assert.False(TemplateVolumeFloorRule.EvaluateSelections(templateId,
            new[] { Slot("duplicate", SyntheticFloorSets), Slot("duplicate", SyntheticSlotFloor) }, floors).IsAllowed);
        Assert.False(TemplateVolumeFloorRule.EvaluateSelections(templateId,
            new[] { Slot(" ", SyntheticFloorSets) }, floors).IsAllowed);
    }

    [Fact]
    public void SelectionFloor_RejectsUnrepresentableTotalWithoutThrowing()
    {
        var selections = new[] { Slot(TemplateVolumeFloorRule.KneeDominantSlotId, int.MaxValue),
            Slot("second", SyntheticSlotFloor) };
        var result = TemplateVolumeFloorRule.EvaluateSelections(
            TemplateVolumeFloorRule.FullBody1DayTemplateId, selections, SyntheticFloors());
        Assert.False(result.IsAllowed);
        Assert.Equal(TemplateVolumeFloorRule.VolumeBelowTemplateFloor, result.LimitationCode);
    }
    private static Dictionary<string, TemplateVolumeFloor> SyntheticFloors()
    {
        var templateId = TemplateVolumeFloorRule.FullBody1DayTemplateId;
        return new Dictionary<string, TemplateVolumeFloor>
        {
            [templateId] = new(templateId, SyntheticFloorSets, SyntheticFloorExercises,
                new Dictionary<string, int> { [TemplateVolumeFloorRule.KneeDominantSlotId] = SyntheticSlotFloor },
                TemplateVolumeFloorRule.ReviewedStatus)
        };
    }

    private static StrengthSlotSelection Slot(string id, int sets) =>
        new(id, default, true, default, false, SyntheticSlotFloor, "Synthetic", null, null,
            sets, null, null, null, null, Array.Empty<string>());}
