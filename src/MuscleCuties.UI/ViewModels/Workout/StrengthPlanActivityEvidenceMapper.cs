using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

internal static class StrengthPlanActivityEvidenceMapper
{
    public static IReadOnlyList<ActivityEligibilityInputV020> Create(
        IReadOnlySet<WorkoutActivityDto> selectedActivities,
        bool? hasSwimmingFacilityAccess,
        bool? canSwimIndependently,
        bool? wantsContinuousSwimming,
        bool? hasRopeClimbingCompetence,
        bool? appropriateClimbingInstructionAvailable,
        bool? isDemandingYogaContent)
    {
        var evidence = new List<ActivityEligibilityInputV020>();
        if (selectedActivities.Contains(WorkoutActivityDto.Swimming))
            evidence.Add(new ActivityEligibilityInputV020(
                WorkoutActivityDto.Swimming,
                HasSwimmingFacilityAccess: hasSwimmingFacilityAccess,
                CanSwimIndependently: canSwimIndependently,
                WantsContinuousSwimming: wantsContinuousSwimming));

        if (selectedActivities.Contains(WorkoutActivityDto.RockClimbing))
            evidence.Add(new ActivityEligibilityInputV020(
                WorkoutActivityDto.RockClimbing,
                HasRopeClimbingCompetence: hasRopeClimbingCompetence,
                AppropriateClimbingInstructionAvailable: appropriateClimbingInstructionAvailable));

        if (selectedActivities.Contains(WorkoutActivityDto.Yoga))
            evidence.Add(new ActivityEligibilityInputV020(
                WorkoutActivityDto.Yoga,
                IsDemandingYogaContent: isDemandingYogaContent));

        return evidence;
    }
}
