using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthTemplateCatalogTests
{
    [Fact]
    public void Catalog_Contains_Required_One_Through_Four_Day_Strength_Splits()
    {
        Assert.Single(StrengthTemplateCatalog.ForSchedule(1));
        Assert.Equal(2, StrengthTemplateCatalog.ForSchedule(2).Count);
        Assert.Equal(3, StrengthTemplateCatalog.ForSchedule(3).Count);
        Assert.Equal(4, StrengthTemplateCatalog.ForSchedule(4).Count);
        Assert.Empty(StrengthTemplateCatalog.ForSchedule(5));
    }

    [Fact]
    public void Variants_Prefer_Different_Patterns_Without_Excluding_Eligible_Alternatives()
    {
        var templates = StrengthTemplateCatalog.ForSchedule(2);
        var firstKnee = templates[0].Slots.Single(slot => slot.SlotId == "knee-dominant");
        var secondKnee = templates[1].Slots.Single(slot => slot.SlotId == "knee-dominant");
        var firstHip = templates[0].Slots.Single(slot => slot.SlotId == "hip-dominant");
        var secondHip = templates[1].Slots.Single(slot => slot.SlotId == "hip-dominant");

        Assert.Contains(MovementPattern.Lunge, firstKnee.AcceptedPatterns);
        Assert.Contains(MovementPattern.SquatPattern, secondKnee.AcceptedPatterns);
        Assert.Contains(MovementPattern.HipThrust, firstHip.AcceptedPatterns);
        Assert.Contains(MovementPattern.HipHinge, secondHip.AcceptedPatterns);
        Assert.Contains(MovementPattern.SquatPattern, firstKnee.PreferredPatterns);
        Assert.Contains(MovementPattern.Lunge, secondKnee.PreferredPatterns);
    }

    [Fact]
    public void Full_Body_High_Intensity_Classifies_Only_Two_Required_Slots_As_Heavy_Mains()
    {
        var fullBody = StrengthTemplateCatalog.ForSchedule(1).Single();
        var heavySlots = fullBody.Slots
            .Where(slot => slot.IsRequired && slot.IsHeavyMainInHighIntensity)
            .ToArray();

        Assert.Equal(2, heavySlots.Length);
        Assert.Equal(StrengthTemplateSlotId.KneeDominant, heavySlots[0].MovementSlot);
        Assert.Equal(StrengthTemplateSlotId.HipDominant, heavySlots[1].MovementSlot);
        Assert.All(fullBody.Slots.Where(slot => slot.IsRequired && !slot.IsHeavyMainInHighIntensity), slot =>
            Assert.Equal(StrengthPrescriptionRole.Accessory, slot.HighIntensityRole));
    }

    [Fact]
    public void Upper_Lower_High_Intensity_Uses_One_Upper_Main_And_Two_Lower_Mains()
    {
        var templates = StrengthTemplateCatalog.ForSchedule(4);
        var upper = templates.Single(template => template.TemplateId == "MC-UL-4D-UPPER-A");
        var lower = templates.Single(template => template.TemplateId == "MC-UL-4D-LOWER-A");

        Assert.Equal("upper-push", Assert.Single(upper.Slots,
            slot => slot.IsHeavyMainInHighIntensity).SlotId);
        Assert.Equal(2, lower.Slots.Count(slot => slot.IsHeavyMainInHighIntensity));
    }

    [Fact]
    public void Required_Coverage_And_Optional_Slots_Are_Explicit()
    {
        var fullBody = StrengthTemplateCatalog.ForSchedule(1).Single();

        Assert.Equal(4, fullBody.Slots.Count(slot => slot.IsRequired));
        Assert.Equal(2, fullBody.Slots.Count(slot => !slot.IsRequired));
        Assert.Equal(new[]
        {
            StrengthTemplateSlotId.KneeDominant,
            StrengthTemplateSlotId.HipDominant,
            StrengthTemplateSlotId.UpperPush,
            StrengthTemplateSlotId.UpperPull
        }, fullBody.Slots.Where(slot => slot.IsRequired).Select(slot => slot.MovementSlot));
    }
}
