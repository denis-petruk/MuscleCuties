using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthTemplateResolverTests
{
    [Fact]
    public void Resolves_Alternative_Patterns_Deterministically_And_Applies_General_Prescription()
    {
        var input = Input(Catalog(
            Candidate(11, MovementPattern.Lunge),
            Candidate(12, MovementPattern.Lunge),
            Candidate(21, MovementPattern.HipThrust),
            Candidate(31, MovementPattern.VerticalPush),
            Candidate(41, MovementPattern.VerticalPull)));

        var first = StrengthTemplateResolver.Resolve(input, maximumWorkingExercises: 5);
        var second = StrengthTemplateResolver.Resolve(input, maximumWorkingExercises: 5);

        Assert.Equal("Resolved", first.Status);
        Assert.Equal(first.Slots.Select(slot => slot.ExerciseId), second.Slots.Select(slot => slot.ExerciseId));
        Assert.Equal(11, first.Slots.Single(slot => slot.SlotId == "knee-dominant").ExerciseId);
        Assert.Equal(21, first.Slots.Single(slot => slot.SlotId == "hip-dominant").ExerciseId);
        Assert.All(first.Slots.Where(slot => slot.IsRequired), slot =>
        {
            Assert.Equal(2, slot.Sets);
            Assert.Equal(8, slot.InitialRepetitionTarget);
            Assert.Equal(3, slot.RepetitionsInReserve);
            Assert.Equal(120, slot.RestSeconds);
        });
    }

    [Fact]
    public void Missing_Reviewed_Catalog_Metadata_Is_Reported_Without_Selecting_Exercise()
    {
        var incomplete = new StrengthExerciseCatalogEntry("Squat", Candidate(1, MovementPattern.SquatPattern).Candidate,
            ["DemandTagsNotReviewed", "SupportedExperiencesMissing"]);

        var result = StrengthTemplateResolver.Resolve(Input([incomplete]), maximumWorkingExercises: 5);
        var knee = result.Slots.Single(slot => slot.SlotId == "knee-dominant");

        Assert.Equal("Limited", result.Status);
        Assert.Null(knee.ExerciseId);
        Assert.Contains("DemandTagsNotReviewed", knee.LimitationCodes);
        Assert.Contains("SupportedExperiencesMissing", knee.LimitationCodes);
        Assert.Contains("RequiredMovementSlotUnresolved", result.LimitationCodes);
    }

    [Fact]
    public void Missing_Catalog_Candidate_Reports_That_Required_Slot_Is_Unavailable()
    {
        var result = StrengthTemplateResolver.Resolve(Input(Catalog()), maximumWorkingExercises: 5);
        var knee = result.Slots.Single(slot => slot.SlotId == "knee-dominant");

        Assert.Equal("Limited", result.Status);
        Assert.Null(knee.ExerciseId);
        Assert.Contains("NoEligibleExercise", knee.LimitationCodes);
        Assert.Contains("RequiredMovementSlotUnresolved", result.LimitationCodes);
    }

    [Fact]
    public void User_Exclusions_And_Equipment_Are_Hard_Filters_With_Specific_Reasons()
    {
        var excluded = Candidate(11, MovementPattern.SquatPattern);
        var equipmentLimitedCandidate = Candidate(12, MovementPattern.SquatPattern).Candidate with
        {
            RequiredEquipment = new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Barbell }
        };
        var equipmentLimited = new StrengthExerciseCatalogEntry("Exercise 12", equipmentLimitedCandidate, []);

        var result = StrengthTemplateResolver.Resolve(
            Input(Catalog(excluded, equipmentLimited), excludedIds: new HashSet<int> { 11 }),
            maximumWorkingExercises: 5);
        var knee = result.Slots.Single(slot => slot.SlotId == "knee-dominant");

        Assert.Null(knee.ExerciseId);
        Assert.Contains(nameof(ExerciseEligibilityReason.UserExcluded), knee.LimitationCodes);
        Assert.Contains(nameof(ExerciseEligibilityReason.EquipmentUnavailable), knee.LimitationCodes);
    }

    [Fact]
    public void Style_Exercise_Maximum_Cannot_Remove_Required_Coverage()
    {
        var input = Input(Catalog());
        var result = StrengthTemplateResolver.Resolve(input, maximumWorkingExercises: 3);

        Assert.Equal("Infeasible", result.Status);
        Assert.Empty(result.Slots);
        Assert.Contains("RequiredCoverageExceedsStyleExerciseMaximum", result.LimitationCodes);
    }

    [Fact]
    public void High_Volume_Uses_Eligible_Main_And_Accessory_Rules_Within_Five_Exercises()
    {
        var input = Input(Catalog(
            Candidate(11, MovementPattern.SquatPattern),
            Candidate(21, MovementPattern.HipHinge),
            Candidate(31, MovementPattern.HorizontalPush),
            Candidate(41, MovementPattern.HorizontalPull),
            Candidate(51, MovementPattern.AntiExtension),
            Candidate(61, MovementPattern.KneeFlexion)),
            activity: WorkoutActivityType.HighVolumeStrength,
            experience: ActivityExperience.Established,
            establishedResistance: true);

        var result = StrengthTemplateResolver.Resolve(input, maximumWorkingExercises: 8);
        var required = result.Slots.Where(slot => slot.IsRequired).ToArray();
        var selected = result.Slots.Where(slot => slot.ExerciseId.HasValue).ToArray();

        Assert.Equal("Resolved", result.Status);
        Assert.Equal(5, selected.Length);
        Assert.All(required.Where(slot => slot.MovementSlot is
            StrengthTemplateSlotId.KneeDominant or StrengthTemplateSlotId.HipDominant), slot =>
        {
            Assert.Equal(StrengthPrescriptionRole.Main, slot.PrescriptionRole);
            Assert.Equal(3, slot.Sets);
            Assert.Equal(8, slot.InitialRepetitionTarget);
            Assert.Equal(150, slot.RestSeconds);
        });
        Assert.All(required.Where(slot => slot.MovementSlot is
            StrengthTemplateSlotId.UpperPush or StrengthTemplateSlotId.UpperPull), slot =>
        {
            Assert.Equal(StrengthPrescriptionRole.Accessory, slot.PrescriptionRole);
            Assert.Equal(2, slot.Sets);
            Assert.Equal(10, slot.InitialRepetitionTarget);
            Assert.Equal(90, slot.RestSeconds);
        });
        var accessory = selected.Single(slot => !slot.IsRequired && slot.ExerciseId.HasValue);
        Assert.Equal(StrengthPrescriptionRole.Accessory, accessory.PrescriptionRole);
        Assert.Equal(2, accessory.Sets);
        Assert.Equal(10, accessory.InitialRepetitionTarget);
        Assert.Equal(90, accessory.RestSeconds);
    }

    [Fact]
    public void HighIntensityCannotBeEnabledByExerciseConfirmations()
    {
        var input = HighIntensityInput() with
        {
            UserConfirmedFamiliarityExerciseIds = new HashSet<int> { 11, 21 },
            UserConfirmedManageableLoadExerciseIds = new HashSet<int> { 11, 21 }
        };
        var result = StrengthTemplateResolver.Resolve(input, maximumWorkingExercises: 8);
        Assert.Equal("Unsupported", result.Status);
        Assert.Empty(result.Slots);
        Assert.Contains(ActivityPrescriptionRule.HighIntensitySaferAlternativeReviewMissing, result.LimitationCodes);
    }

    [Fact]
    public void HighIntensityWithoutConfirmationsAlsoRequiresReview()
    {
        var result = StrengthTemplateResolver.Resolve(HighIntensityInput(excludedIds: new HashSet<int> { 11 }),
            maximumWorkingExercises: 8);
        Assert.Equal("Unsupported", result.Status);
        Assert.Empty(result.Slots);
        Assert.Contains(ActivityPrescriptionRule.HighIntensitySaferAlternativeReviewMissing, result.LimitationCodes);
    }
    private static StrengthTemplateSelectionInput HighIntensityInput(
        IReadOnlySet<int>? excludedIds = null) => Input(Catalog(
            Candidate(11, MovementPattern.SquatPattern),
            Candidate(21, MovementPattern.HipHinge),
            Candidate(31, MovementPattern.HorizontalPush),
            Candidate(41, MovementPattern.HorizontalPull)),
        excludedIds: excludedIds,
        activity: WorkoutActivityType.StrengthHighIntensity,
        experience: ActivityExperience.Established,
        establishedResistance: true) with
    {
        CompletedExerciseExposureCounts = new Dictionary<int, int> { [11] = 4, [21] = 4 },
        KnownManageableLoadExerciseIds = new HashSet<int> { 11, 21 }
    };

    [Fact]
    public void Preferred_Substitute_Only_Gains_Priority_After_Directed_Pair_Conditions_Are_Met()
    {
        var original = Candidate(11, MovementPattern.SquatPattern) with
        {
            Candidate = Candidate(11, MovementPattern.SquatPattern).Candidate with
            {
                DirectedSubstitutionReviews =
                [new(12, DirectedSubstitutionCategory.CloseProgrammingAlternative,
                    "Separate load and setup review required")]
            }
        };
        var catalog = Catalog(original, Candidate(12, MovementPattern.SquatPattern),
            Candidate(21, MovementPattern.HipHinge),
            Candidate(31, MovementPattern.HorizontalPush),
            Candidate(41, MovementPattern.HorizontalPull));
        var input = Input(catalog) with
        {
            PreferredExerciseByOriginalId = new Dictionary<int, int> { [11] = 12 }
        };

        var unacknowledged = StrengthTemplateResolver.Resolve(input, 5);
        var acknowledged = StrengthTemplateResolver.Resolve(input with
        {
            EligibilityContext = input.EligibilityContext with
            {
                AcknowledgedSubstitutionPairKeys = new HashSet<string> { "11:12" }
            }
        }, 5);

        Assert.Equal(11, unacknowledged.Slots.Single(slot => slot.SlotId == "knee-dominant").ExerciseId);
        Assert.Equal(12, acknowledged.Slots.Single(slot => slot.SlotId == "knee-dominant").ExerciseId);
    }

    private static StrengthTemplateSelectionInput Input(
        IReadOnlyList<StrengthExerciseCatalogEntry> catalog,
        IReadOnlySet<int>? excludedIds = null,
        WorkoutActivityType activity = WorkoutActivityType.GeneralStrength,
        ActivityExperience experience = ActivityExperience.Beginner,
        bool establishedResistance = false)
    {
        return new StrengthTemplateSelectionInput(
            StrengthTemplateCatalog.ForSchedule(1).Single(),
            activity,
            UserGoal.Strength,
            new ActivityPrescriptionContext(activity, experience, null, false, true,
                establishedResistance, 0, false, false),
            catalog,
            new ExerciseEligibilityContext(
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                experience,
                excludedIds ?? new HashSet<int>(),
                new HashSet<MovementPattern>(),
                new HashSet<ActivityDemandTag>(), false),
            new Dictionary<int, int>(),
            new HashSet<int>(),
            new HashSet<int>());
    }

    private static IReadOnlyList<StrengthExerciseCatalogEntry> Catalog(
        params StrengthExerciseCatalogEntry[] entries) => entries;

    private static StrengthExerciseCatalogEntry Candidate(int id, MovementPattern pattern) =>
        new($"Exercise {id}",
            new ReviewedExerciseCandidate(id, "test-v2", pattern,
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                new HashSet<ActivityExperience>
                    { ActivityExperience.Beginner, ActivityExperience.Established },
                new HashSet<ActivityDemandTag> { ActivityDemandTag.Mobility },
                true, true, new HashSet<ActivityDemandTag>(),
                new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
                1m, new HashSet<int>(), InjuryFlag.None), []);
}
