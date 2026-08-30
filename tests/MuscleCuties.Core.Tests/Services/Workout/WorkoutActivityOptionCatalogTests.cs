using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.Tests.Services.Workout;

public class WorkoutActivityOptionCatalogTests
{
    [Fact]
    public void Build_WithEmptySelectionSelectsStrengthAndRecoveryOnly()
    {
        var options = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());

        Assert.Contains(options, option =>
            option.ActivityType == WorkoutActivityType.HighVolumeStrength && option.IsSelected);
        Assert.Contains(options, option =>
            option.ActivityType == WorkoutActivityType.Yoga && option.IsSelected);
        Assert.DoesNotContain(options, option =>
            WorkoutActivityPreferences.IsCardioActivity(option.ActivityType) && option.IsSelected);
    }

    [Fact]
    public void BuildGroups_ReturnsOrderedSectionsWithIcons()
    {
        var groups = WorkoutActivityOptionCatalog.BuildGroups(
            WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>()));

        Assert.Collection(
            groups,
            strength =>
            {
                Assert.Equal("Strength", strength.GroupTitle);
                Assert.Equal("Dumbbell24", strength.GroupIconGlyph);
                Assert.Equal(3, strength.Items.Count);
            },
            cardio =>
            {
                Assert.Equal("Cardio", cardio.GroupTitle);
                Assert.Equal("PulseSquare24", cardio.GroupIconGlyph);
                Assert.Equal(4, cardio.Items.Count);
            },
            recovery =>
            {
                Assert.Equal("Recovery", recovery.GroupTitle);
                Assert.Equal("LeafThree24", recovery.GroupIconGlyph);
                Assert.Single(recovery.Items);
            });
    }

    [Fact]
    public void ToggleSelection_KeepsRequiredFallbacks()
    {
        var options = WorkoutActivityOptionCatalog.Build(new HashSet<WorkoutActivityType>());
        var strength = options.Single(option => option.ActivityType == WorkoutActivityType.HighVolumeStrength);
        var recovery = options.Single(option => option.ActivityType == WorkoutActivityType.Yoga);

        var strengthMessage = WorkoutActivityOptionCatalog.ToggleSelection(options, strength);
        var recoveryMessage = WorkoutActivityOptionCatalog.ToggleSelection(options, recovery);

        Assert.True(strength.IsSelected);
        Assert.True(recovery.IsSelected);
        Assert.Contains("strength", strengthMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recovery", recoveryMessage, StringComparison.OrdinalIgnoreCase);
    }
}
