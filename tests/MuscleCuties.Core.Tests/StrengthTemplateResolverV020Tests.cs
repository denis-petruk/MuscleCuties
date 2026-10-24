using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class StrengthTemplateResolverV020Tests
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

        var first = StrengthTemplateResolverV020.Resolve(input, maximumWorkingExercises: 5);
        var second = StrengthTemplateResolverV020.Resolve(input, maximumWorkingExercises: 5);

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
        var incomplete = new StrengthExerciseCatalogEntryV020("Squat", Candidate(1, MovementPattern.SquatPattern).Candidate,
            ["DemandTagsNotReviewed", "SupportedExperiencesMissing"]);

        var result = StrengthTemplateResolverV020.Resolve(Input([incomplete]), maximumWorkingExercises: 5);
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
        var result = StrengthTemplateResolverV020.Resolve(Input(Catalog()), maximumWorkingExercises: 5);
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
        var equipmentLimited = new StrengthExerciseCatalogEntryV020("Exercise 12", equipmentLimitedCandidate, []);

        var result = StrengthTemplateResolverV020.Resolve(
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
        var result = StrengthTemplateResolverV020.Resolve(input, maximumWorkingExercises: 3);

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
            experience: ActivityExperienceV020.Established,
            establishedResistance: true);

        var result = StrengthTemplateResolverV020.Resolve(input, maximumWorkingExercises: 8);
        var required = result.Slots.Where(slot => slot.IsRequired).ToArray();
        var selected = result.Slots.Where(slot => slot.ExerciseId.HasValue).ToArray();

        Assert.Equal("Resolved", result.Status);
        Assert.Equal(5, selected.Length);
        Assert.All(required.Where(slot => slot.MovementSlot is
            StrengthTemplateSlotIdV020.KneeDominant or StrengthTemplateSlotIdV020.HipDominant), slot =>
        {
            Assert.Equal(StrengthPrescriptionRoleV020.Main, slot.PrescriptionRole);
            Assert.Equal(3, slot.Sets);
            Assert.Equal(8, slot.InitialRepetitionTarget);
            Assert.Equal(150, slot.RestSeconds);
        });
        Assert.All(required.Where(slot => slot.MovementSlot is
            StrengthTemplateSlotIdV020.UpperPush or StrengthTemplateSlotIdV020.UpperPull), slot =>
        {
            Assert.Equal(StrengthPrescriptionRoleV020.Accessory, slot.PrescriptionRole);
            Assert.Equal(2, slot.Sets);
            Assert.Equal(10, slot.InitialRepetitionTarget);
            Assert.Equal(90, slot.RestSeconds);
        });
        var accessory = selected.Single(slot => !slot.IsRequired && slot.ExerciseId.HasValue);
        Assert.Equal(StrengthPrescriptionRoleV020.Accessory, accessory.PrescriptionRole);
        Assert.Equal(2, accessory.Sets);
        Assert.Equal(10, accessory.InitialRepetitionTarget);
        Assert.Equal(90, accessory.RestSeconds);
    }

    [Fact]
    public void High_Intensity_Uses_Only_Qualified_Heavy_Mains_And_At_Most_Four_Exercises()
    {
        var input = Input(Catalog(
                Candidate(11, MovementPattern.SquatPattern),
                Candidate(21, MovementPattern.HipHinge),
                Candidate(31, MovementPattern.HorizontalPush),
                Candidate(41, MovementPattern.HorizontalPull)),
            activity: WorkoutActivityType.StrengthHighIntensity,
            experience: ActivityExperienceV020.Established,
            establishedResistance: true) with
        {
            CompletedExerciseExposureCounts = new Dictionary<int, int> { [11] = 4, [21] = 4 },
            KnownManageableLoadExerciseIds = new HashSet<int> { 11, 21 },
            UserConfirmedFamiliarityExerciseIds = new HashSet<int> { 11, 21 }
        };

        var result = StrengthTemplateResolverV020.Resolve(input, maximumWorkingExercises: 8);
        var selected = result.Slots.Where(slot => slot.ExerciseId.HasValue).ToArray();
        var heavy = selected.Where(slot => slot.IsHeavyMain).ToArray();
        var accessories = selected.Where(slot => !slot.IsHeavyMain).ToArray();

        Assert.Equal("Resolved", result.Status);
        Assert.Equal(4, selected.Length);
        Assert.Equal(2, heavy.Length);
        Assert.All(heavy, slot =>
        {
            Assert.Equal(StrengthPrescriptionRoleV020.Main, slot.PrescriptionRole);
            Assert.Equal(3, slot.Sets);
            Assert.Equal(4, slot.InitialRepetitionTarget);
            Assert.Equal(180, slot.RestSeconds);
        });
        Assert.All(accessories, slot =>
        {
            Assert.Equal(StrengthPrescriptionRoleV020.Accessory, slot.PrescriptionRole);
            Assert.Equal(2, slot.Sets);
            Assert.Equal(10, slot.InitialRepetitionTarget);
            Assert.Equal(90, slot.RestSeconds);
        });
    }

    [Fact]
    public void Preferred_Substitute_Only_Gains_Priority_After_Directed_Pair_Conditions_Are_Met()
    {
        var original = Candidate(11, MovementPattern.SquatPattern) with
        {
            Candidate = Candidate(11, MovementPattern.SquatPattern).Candidate with
            {
                DirectedSubstitutionReviewsV020 =
                [new(12, DirectedSubstitutionCategoryV020.CloseProgrammingAlternative,
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

        var unacknowledged = StrengthTemplateResolverV020.Resolve(input, 5);
        var acknowledged = StrengthTemplateResolverV020.Resolve(input with
        {
            EligibilityContext = input.EligibilityContext with
            {
                AcknowledgedSubstitutionPairKeys = new HashSet<string> { "11:12" }
            }
        }, 5);

        Assert.Equal(11, unacknowledged.Slots.Single(slot => slot.SlotId == "knee-dominant").ExerciseId);
        Assert.Equal(12, acknowledged.Slots.Single(slot => slot.SlotId == "knee-dominant").ExerciseId);
    }

    private static StrengthTemplateSelectionInputV020 Input(
        IReadOnlyList<StrengthExerciseCatalogEntryV020> catalog,
        IReadOnlySet<int>? excludedIds = null,
        WorkoutActivityType activity = WorkoutActivityType.GeneralStrength,
        ActivityExperienceV020 experience = ActivityExperienceV020.Beginner,
        bool establishedResistance = false)
    {
        return new StrengthTemplateSelectionInputV020(
            StrengthTemplateCatalogV020.ForSchedule(1).Single(),
            activity,
            UserGoal.Strength,
            new ActivityPrescriptionContextV020(activity, experience, null, false, true,
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

    private static IReadOnlyList<StrengthExerciseCatalogEntryV020> Catalog(
        params StrengthExerciseCatalogEntryV020[] entries) => entries;

    private static StrengthExerciseCatalogEntryV020 Candidate(int id, MovementPattern pattern) =>
        new($"Exercise {id}",
            new ReviewedExerciseCandidate(id, "test-v2", pattern,
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                new HashSet<ActivityExperienceV020>
                    { ActivityExperienceV020.Beginner, ActivityExperienceV020.Established },
                new HashSet<ActivityDemandTag> { ActivityDemandTag.Mobility },
                true, true, new HashSet<ActivityDemandTag>(),
                new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
                1m, new HashSet<int>(), InjuryFlag.None), []);
}
