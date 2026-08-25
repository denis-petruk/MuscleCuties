using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner
{
    private static IReadOnlyList<SessionTemplate> BuildSessionTemplates(
        UserProfile profile,
        CyclePhase phase,
        int trainingDays)
    {
        var templates = phase switch
        {
            CyclePhase.Menstrual => BuildMenstrualTemplates(),
            CyclePhase.Luteal => BuildLutealTemplates(profile.Goal),
            CyclePhase.Ovulatory => BuildOvulatoryTemplates(profile.Goal),
            _ => BuildFollicularTemplates(profile.Goal)
        };

        var preferences = WorkoutActivityPreferences.Parse(profile.PreferredWorkoutActivityTypes);
        if (preferences.Count > 0)
            templates = MergePreferredTemplates(BuildPreferredTemplates(preferences, profile.Goal), templates);

        return templates.Take(trainingDays).ToList();
    }

    private static IReadOnlyList<SessionTemplate> MergePreferredTemplates(
        IReadOnlyList<SessionTemplate> preferredTemplates,
        IReadOnlyList<SessionTemplate> phaseTemplates)
    {
        if (preferredTemplates.Count == 0)
            return phaseTemplates;

        return preferredTemplates
            .Concat(phaseTemplates)
            .DistinctBy(template => template.Name)
            .ToList();
    }

    private static IReadOnlyList<SessionTemplate> BuildPreferredTemplates(
        IReadOnlySet<WorkoutActivityType> preferences,
        UserGoal goal)
    {
        var templates = new List<SessionTemplate>();

        if (preferences.Contains(WorkoutActivityType.Strength))
            templates.Add(goal is UserGoal.Strength
                ? Strength("Power Builder", "Goblet Squat", "Hip Thrust", "Romanian Deadlift", "Dumbbell Row")
                : Strength("Strong Glow", "Goblet Squat", "Incline Push-Up", "Dumbbell Row", "Glute Bridge"));

        if (preferences.Contains(WorkoutActivityType.ActiveRecovery))
            templates.Add(Timed("Easy Reset Flow", "Active Recovery Flow", "Easy Walk", "Mobility Flow"));

        if (preferences.Contains(WorkoutActivityType.RockClimbing))
            templates.Add(Timed("Climb Strong", "Rock Climbing", "Mobility Flow", "Dead Bug"));

        if (preferences.Contains(WorkoutActivityType.CardioIntervals))
            templates.Add(Timed("Fast Spark", "Bike Intervals", "Pallof Press", "Plank"));

        if (preferences.Contains(WorkoutActivityType.ZoneTwoCardio) ||
            preferences.Contains(WorkoutActivityType.Cycling))
        {
            templates.Add(Timed("Smooth Ride", "Zone 2 Ride", "Mobility Flow", "Dead Bug"));
        }

        if (preferences.Contains(WorkoutActivityType.Walking))
            templates.Add(Timed("Clear Head Walk", "Easy Walk", "Mobility Flow"));

        if (preferences.Contains(WorkoutActivityType.Swimming))
            templates.Add(Timed("Pool Glow", "Swimming", "Mobility Flow"));

        if (preferences.Contains(WorkoutActivityType.Dance))
            templates.Add(Timed("Dance Cardio", "Dance Cardio", "Mobility Flow"));

        if (preferences.Contains(WorkoutActivityType.YogaFlow))
            templates.Add(Timed("Flow State Yoga", "Yoga Flow", "Mobility Flow", "Easy Walk"));

        if (preferences.Contains(WorkoutActivityType.PowerYoga))
            templates.Add(Timed("Power Yoga Glow", "Power Yoga", "Side Plank", "Bird Dog"));

        if (preferences.Contains(WorkoutActivityType.YinYoga))
            templates.Add(Timed("Deep Ease Yoga", "Yin Yoga", "Breathing Reset"));

        if (preferences.Contains(WorkoutActivityType.RestorativeYoga))
            templates.Add(Timed("Soft Landing Yoga", "Restorative Yoga", "Breathing Reset"));

        if (preferences.Contains(WorkoutActivityType.Pilates))
            templates.Add(Timed("Core Line Pilates", "Pilates Flow", "Side Plank", "Dead Bug"));

        if (preferences.Contains(WorkoutActivityType.Mobility))
            templates.Add(Timed("Mobility Reset", "Mobility Flow", "Bird Dog", "Dead Bug"));

        return templates;
    }

    private static IReadOnlyList<SessionTemplate> BuildMenstrualTemplates() =>
    [
        Timed("Soft Reset", "Mobility Flow", "Easy Walk", "Dead Bug"),
        Strength("Grace Strength", "Glute Bridge", "Incline Push-Up", "Dumbbell Row", "Bird Dog"),
        Timed("Quiet Flow", "Yoga Flow", "Side Plank", "Easy Walk"),
        Strength("Light Leg Glow", "Step-Up", "Glute Bridge", "Calf Raise", "Pallof Press"),
        Timed("Happy Pace", "Zone 2 Ride", "Mobility Flow", "Dead Bug")
    ];

    private static IReadOnlyList<SessionTemplate> BuildFollicularTemplates(UserGoal goal)
    {
        var templates = new List<SessionTemplate>
        {
            Strength("Leg Day Glow", "Goblet Squat", "Hip Thrust", "Romanian Deadlift", "Step-Up", "Calf Raise"),
            Strength("Upper Body Glow", "Incline Push-Up", "Dumbbell Row", "Overhead Press", "Lateral Raise", "Face Pull"),
            Strength("Glute Core Glow", "Hip Thrust", "Reverse Lunge", "Romanian Deadlift", "Dead Bug", "Side Plank")
        };

        if (goal is UserGoal.FatLoss)
            templates.Add(Timed("Sweat Spark", "Bike Intervals", "Pallof Press", "Plank"));
        else
            templates.Add(Strength("Full Body Bloom", "Goblet Squat", "Incline Dumbbell Press", "Seated Cable Row", "Reverse Lunge"));

        templates.Add(Strength("Upper Body Spark", "Lat Pulldown", "Lateral Raise", "Biceps Curl", "Triceps Pressdown", "Face Pull"));
        templates.Add(Timed("Smooth Ride", "Zone 2 Ride", "Mobility Flow", "Plank"));
        return templates;
    }

    private static IReadOnlyList<SessionTemplate> BuildOvulatoryTemplates(UserGoal goal)
    {
        var templates = new List<SessionTemplate>
        {
            Strength("Peak Power Legs", "Goblet Squat", "Hip Thrust", "Romanian Deadlift", "Reverse Lunge", "Calf Raise"),
            Strength("Peak Power Push", "Incline Dumbbell Press", "Dumbbell Row", "Overhead Press", "Lat Pulldown", "Face Pull"),
            Timed(goal is UserGoal.FatLoss ? "Sweat Sprint" : "Peak Cardio", "Bike Intervals", "Plank", "Pallof Press"),
            Strength("Glute Power Day", "Hip Thrust", "Step-Up", "Romanian Deadlift", "Side Plank")
        };

        templates.Add(Strength("Upper Body Spark", "Seated Cable Row", "Lateral Raise", "Biceps Curl", "Triceps Pressdown"));
        templates.Add(Timed("Victory Ride", "Zone 2 Ride", "Mobility Flow", "Dead Bug"));
        return templates;
    }

    private static IReadOnlyList<SessionTemplate> BuildLutealTemplates(UserGoal goal)
    {
        var templates = new List<SessionTemplate>
        {
            Strength("Steady Legs", "Goblet Squat", "Glute Bridge", "Step-Up", "Dead Bug"),
            Strength("Steady Upper", "Incline Push-Up", "Dumbbell Row", "Lateral Raise", "Face Pull"),
            Timed("Smooth Ride", "Zone 2 Ride", "Mobility Flow", "Side Plank"),
            Strength(goal is UserGoal.Strength ? "Skill Builder Flow" : "Tone Builder Flow", "Reverse Lunge", "Seated Cable Row", "Overhead Press", "Pallof Press"),
            Timed("Soft Flow", "Yoga Flow", "Easy Walk", "Bird Dog")
        };

        return templates;
    }

    private static SessionTemplate Strength(string name, params string[] exerciseNames) =>
        new(name, false, exerciseNames);

    private static SessionTemplate Timed(string name, params string[] exerciseNames) =>
        new(name, true, exerciseNames);

    private sealed record SessionTemplate(
        string Name,
        bool IsTimed,
        IReadOnlyList<string> ExerciseNames);
}
