using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Gate 56: a user-selected glute focus changes which exercise the planner picks, and selecting
/// nothing keeps the previous behaviour exactly. The flag is consumed by the resolver itself, and
/// only the profile can supply it.
/// </summary>
public sealed class GluteFocusSelectionTests
{
    private const int HingeExerciseId = 21;
    private const int ThrustExerciseId = 22;

    [Fact]
    public void Without_a_saved_focus_the_slot_keeps_its_previous_choice()
    {
        var result = StrengthTemplateResolver.Resolve(Input(gluteFocus: false), maximumWorkingExercises: 5);

        Assert.Equal("Resolved", result.Status);
        Assert.Equal(HingeExerciseId, result.Slots.Single(slot => slot.SlotId == "hip-dominant").ExerciseId);
    }

    [Fact]
    public void A_saved_glute_focus_selects_the_glute_dominant_movement()
    {
        var result = StrengthTemplateResolver.Resolve(Input(gluteFocus: true), maximumWorkingExercises: 5);

        Assert.Equal("Resolved", result.Status);
        var hipSlot = result.Slots.Single(slot => slot.SlotId == "hip-dominant");
        Assert.Equal(ThrustExerciseId, hipSlot.ExerciseId);
        Assert.Equal("Hip thrust option", hipSlot.ExerciseName);
    }

    [Fact]
    public void The_focus_leaves_slots_without_a_glute_option_unchanged()
    {
        var without = StrengthTemplateResolver.Resolve(Input(gluteFocus: false), maximumWorkingExercises: 5);
        var with = StrengthTemplateResolver.Resolve(Input(gluteFocus: true), maximumWorkingExercises: 5);

        Assert.Equal(Slot(without, "knee-dominant").ExerciseId, Slot(with, "knee-dominant").ExerciseId);
        Assert.Equal(Slot(without, "upper-push").ExerciseId, Slot(with, "upper-push").ExerciseId);
        Assert.Equal(Slot(without, "upper-pull").ExerciseId, Slot(with, "upper-pull").ExerciseId);
    }

    private static StrengthSlotSelection Slot(StrengthTemplateResolution resolution, string slotId) =>
        resolution.Slots.Single(slot => slot.SlotId == slotId);

    private static StrengthTemplateSelectionInput Input(bool gluteFocus)
    {
        const WorkoutActivityType activity = WorkoutActivityType.GeneralStrength;
        const ActivityExperience experience = ActivityExperience.Beginner;
        var catalog = new List<StrengthExerciseCatalogEntry>
        {
            Candidate(HingeExerciseId, MovementPattern.HipHinge, "Hinge option"),
            Candidate(ThrustExerciseId, MovementPattern.HipThrust, "Hip thrust option"),
            Candidate(11, MovementPattern.Lunge, "Lunge"),
            Candidate(31, MovementPattern.VerticalPush, "Push"),
            Candidate(41, MovementPattern.VerticalPull, "Pull")
        };

        return new StrengthTemplateSelectionInput(
            StrengthTemplateCatalog.ForSchedule(1).Single(),
            activity,
            UserGoal.Strength,
            new ActivityPrescriptionContext(activity, experience, null, false, true, false, 0, false, false),
            catalog,
            new ExerciseEligibilityContext(
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                experience,
                new HashSet<int>(),
                new HashSet<MovementPattern>(),
                new HashSet<ActivityDemandTag>(),
                false),
            new Dictionary<int, int>(),
            new HashSet<int>(),
            new HashSet<int>(),
            GluteFocus: gluteFocus);
    }

    private static StrengthExerciseCatalogEntry Candidate(int id, MovementPattern pattern, string name) =>
        new(name,
            new ReviewedExerciseCandidate(id, "test-v2", pattern,
                new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
                new HashSet<ActivityExperience> { ActivityExperience.Beginner, ActivityExperience.Established },
                new HashSet<ActivityDemandTag> { ActivityDemandTag.Mobility },
                true, true, new HashSet<ActivityDemandTag>(),
                new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
                1m, new HashSet<int>(), InjuryFlag.None), []);
}
