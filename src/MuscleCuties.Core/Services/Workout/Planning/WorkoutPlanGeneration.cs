using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner
{
    public bool ShouldReplaceGeneratedPlan(
        WorkoutPlan? activePlan,
        IReadOnlyCollection<WorkoutDay> activePlanDays,
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        CyclePhase phase)
    {
        if (activePlan is null)
            return true;

        if (!IsGeneratedPlan(activePlan))
            return false;

        return activePlan.CyclePhaseTarget != phase ||
               activePlan.Name != BuildPlanName(profile, snapshot, phase) ||
               activePlanDays.Count != 7 ||
               activePlanDays.Count(day => day.WorkoutType != WorkoutType.Rest) != CalculateTrainingDays(profile, snapshot, phase) ||
               activePlanDays.Any(day => day.WorkoutType != WorkoutType.Rest && day.WorkoutDayExercises.Count == 0);
    }

    public WorkoutPlan BuildGeneratedPlan(
        int userId,
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        IReadOnlyCollection<Exercise> exerciseLibrary,
        CyclePhase phase,
        DateTime createdAt)
    {
        var trainingDays = CalculateTrainingDays(profile, snapshot, phase);
        var schedule = BuildSchedule(trainingDays);
        var prescription = BuildPrescription(profile, snapshot, phase);
        var templates = BuildSessionTemplates(profile, phase, trainingDays);
        var exercisesByName = exerciseLibrary.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        var plan = new WorkoutPlan
        {
            UserId = userId,
            Name = BuildPlanName(profile, snapshot, phase),
            IsActive = true,
            CyclePhaseTarget = phase,
            CreatedAt = createdAt
        };

        var templateByDay = templates
            .Select((template, index) => new { DayOfWeek = schedule[index], Template = template })
            .ToDictionary(item => item.DayOfWeek, item => item.Template);

        for (var dayOfWeek = 0; dayOfWeek < 7; dayOfWeek++)
        {
            if (!templateByDay.TryGetValue(dayOfWeek, out var template))
            {
                plan.WorkoutDays.Add(new WorkoutDay
                {
                    DayOfWeek = dayOfWeek,
                    WorkoutType = WorkoutType.Rest,
                    Name = "Living happy life"
                });
                continue;
            }

            var day = new WorkoutDay
            {
                DayOfWeek = dayOfWeek,
                WorkoutType = BuildWorkoutType(template),
                Name = template.Name
            };

            foreach (var exerciseName in template.ExerciseNames)
            {
                if (!exercisesByName.TryGetValue(exerciseName, out var exercise))
                    continue;

                day.WorkoutDayExercises.Add(new WorkoutDayExercise
                {
                    ExerciseId = exercise.Id,
                    Sets = template.IsTimed ? 0 : prescription.Sets,
                    Reps = template.IsTimed ? 0 : prescription.Reps,
                    DurationSeconds = template.IsTimed ? prescription.TimedExerciseSeconds : null
                });
            }

            plan.WorkoutDays.Add(day);
        }

        return plan;
    }

    private static bool IsGeneratedPlan(WorkoutPlan plan) =>
        plan.Name.StartsWith(GeneratedPlanPrefix, StringComparison.OrdinalIgnoreCase) ||
        plan.Name.StartsWith(LegacyGeneratedPlanPrefix, StringComparison.OrdinalIgnoreCase);

    private static WorkoutType BuildWorkoutType(SessionTemplate template)
    {
        if (template.Name.Contains("Recovery", StringComparison.OrdinalIgnoreCase) ||
            template.Name.Contains("Mobility", StringComparison.OrdinalIgnoreCase) ||
            template.Name.Contains("Yoga", StringComparison.OrdinalIgnoreCase) ||
            template.Name.Contains("Pilates", StringComparison.OrdinalIgnoreCase))
        {
            return WorkoutType.Recovery;
        }

        return template.IsTimed ? WorkoutType.Cardio : WorkoutType.Strength;
    }

    private static string BuildPlanName(
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        CyclePhase phase)
    {
        var days = CalculateTrainingDays(profile, snapshot, phase);
        var focus = profile.Goal switch
        {
            UserGoal.FatLoss => "Sweat Spark",
            UserGoal.Strength => "Power Bloom",
            UserGoal.MaintainHealth => "Feel Good Flow",
            _ => "Tone Glow"
        };

        return $"{GeneratedPlanPrefix} {focus} {FormatTrainingDayCount(days)}";
    }

    private static string FormatTrainingDayCount(int days) => days switch
    {
        1 => "One Day",
        2 => "Two Days",
        3 => "Three Days",
        4 => "Four Days",
        5 => "Five Days",
        6 => "Six Days",
        _ => $"{days} Days"
    };

    private static int CalculateTrainingDays(
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        CyclePhase phase)
    {
        var requestedDays = profile.WorkoutDaysPerWeek <= 0 ? 3 : Math.Clamp(profile.WorkoutDaysPerWeek, 1, 6);
        var baseline = ReadPhaseBaseline(snapshot, phase);
        var needsDeload = phase is CyclePhase.Menstrual ||
                          phase is CyclePhase.Luteal ||
                          baseline.Pain >= 4 ||
                          baseline.Energy is > 0 and <= 2;

        if (needsDeload && requestedDays > 2)
            requestedDays -= 1;

        return requestedDays;
    }

    private static WorkoutPrescription BuildPrescription(
        UserProfile profile,
        UserProfileSnapshot? snapshot,
        CyclePhase phase)
    {
        var baseline = ReadPhaseBaseline(snapshot, phase);
        var sets = profile.TrainingExperienceLevel switch
        {
            TrainingExperienceLevel.Advanced => 4,
            TrainingExperienceLevel.Intermediate => 3,
            _ => 2
        };

        var reps = profile.Goal switch
        {
            UserGoal.Strength => profile.TrainingExperienceLevel is TrainingExperienceLevel.Beginner ? 8 : 6,
            UserGoal.FatLoss => 14,
            UserGoal.MuscleTone => 12,
            _ => 10
        };

        var timedSeconds = profile.Goal is UserGoal.FatLoss ? 900 : 600;
        var needsDeload = phase is CyclePhase.Menstrual ||
                          phase is CyclePhase.Luteal ||
                          baseline.Pain >= 4 ||
                          baseline.Energy is > 0 and <= 2;

        if (phase is CyclePhase.Ovulatory && baseline.Pain <= 2 && baseline.Energy >= 4)
        {
            sets += profile.TrainingExperienceLevel is TrainingExperienceLevel.Beginner ? 0 : 1;
            timedSeconds += 180;
        }

        if (needsDeload)
        {
            sets = Math.Max(2, sets - 1);
            reps = Math.Max(8, reps - 2);
            timedSeconds = Math.Max(420, timedSeconds - 180);
        }

        return new WorkoutPrescription(sets, reps, timedSeconds);
    }

    private static int[] BuildSchedule(int trainingDays) =>
        trainingDays switch
        {
            <= 1 => [1],
            2 => [1, 4],
            3 => [1, 3, 5],
            4 => [1, 2, 4, 6],
            5 => [1, 2, 4, 5, 6],
            _ => [1, 2, 3, 4, 5, 6]
        };

    private sealed record WorkoutPrescription(int Sets, int Reps, int TimedExerciseSeconds);
}
