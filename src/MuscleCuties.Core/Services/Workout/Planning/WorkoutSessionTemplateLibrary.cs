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
        UserProfileSnapshot? snapshot,
        CyclePhase phase,
        int trainingDays)
    {
        if (trainingDays <= 0)
            return [];

        var baseline = ReadPhaseBaseline(snapshot, phase);
        var preferences = WorkoutActivityPreferences.Parse(profile.PreferredWorkoutActivityTypes).ToHashSet();
        var hasExplicitPreferences = preferences.Count > 0;
        if (preferences.Count == 0)
            preferences.UnionWith(BuildDefaultPreferences(profile));

        if (hasExplicitPreferences && profile.Goal is UserGoal.Strength && !preferences.Any(IsStrengthActivity))
            preferences.Add(WorkoutActivityType.HighVolumeStrength);

        var strengthStyle = WorkoutActivityPreferences.ParseStrengthStyle(profile.PreferredWorkoutActivityTypes);
        var preferredTemplates = hasExplicitPreferences
            ? phase is CyclePhase.Menstrual && baseline.Energy < 4
                ? BuildMenstrualPreferredTemplates(preferences)
                : BuildPreferredTemplates(preferences, profile, strengthStyle)
            : [];
        var phaseTemplates = BuildPhaseTemplates(profile, phase);
        var candidates = preferredTemplates
            .Concat(phaseTemplates)
            .DistinctBy(template => template.Name)
            .ToList();
        var slots = BuildWeeklySlots(trainingDays, phase, baseline);
        var selected = new List<SessionTemplate>();

        foreach (var slot in slots)
        {
            selected.Add(BuildTemplateForSlot(
                slot,
                candidates,
                phaseTemplates,
                selected,
                phase,
                profile.Goal,
                baseline,
                strengthStyle));
        }

        return selected.Take(trainingDays).ToList();
    }

    private static IReadOnlySet<WorkoutActivityType> BuildDefaultPreferences(UserProfile profile)
    {
        var defaults = new HashSet<WorkoutActivityType>
        {
            WorkoutActivityType.HighVolumeStrength,
            WorkoutActivityType.Cycling,
            WorkoutActivityType.Yoga
        };

        if (profile.Goal is UserGoal.FatLoss)
            defaults.Add(WorkoutActivityType.Hiit);

        if (profile.Goal is UserGoal.Strength)
            defaults.Add(WorkoutActivityType.StrengthHighIntensity);

        return defaults;
    }

    private static IReadOnlyList<SessionTemplate> BuildPreferredTemplates(
        IReadOnlySet<WorkoutActivityType> preferences,
        UserProfile profile,
        StrengthTrainingStyle strengthStyle)
    {
        var templates = new List<SessionTemplate>();
        var goal = profile.Goal;
        var isAdvanced = IsAdvancedPhysiqueProfile(profile);

        if (preferences.Contains(WorkoutActivityType.StrengthHighIntensity))
        {
            templates.Add(Strength(
                WorkoutActivityType.StrengthHighIntensity,
                goal is UserGoal.Strength ? "Heavy strength training" : "Focused strength training",
                4,
                isAdvanced
                    ? ["Barbell Hip Thrust", "Leg Press", "Assisted Pull-Up", "Chest Supported Row", "Cable Lateral Raise", "Pallof Press"]
                    : ["Goblet Squat", "Romanian Deadlift", "Incline Dumbbell Press", "Dumbbell Row", "Pallof Press"]));
        }

        if (preferences.Contains(WorkoutActivityType.HighVolumeStrength))
        {
            templates.Add((goal, strengthStyle) switch
            {
                (UserGoal.Strength, StrengthTrainingStyle.ExpressHard) =>
                    Strength(
                        WorkoutActivityType.StrengthHighIntensity,
                        "Express heavy strength training",
                        4,
                        isAdvanced
                            ? ["Barbell Hip Thrust", "Leg Press", "Assisted Pull-Up", "Chest Supported Row", "Cable Lateral Raise", "Pallof Press"]
                            : ["Goblet Squat", "Romanian Deadlift", "Incline Dumbbell Press", "Dumbbell Row", "Pallof Press"]),
                (UserGoal.Strength, _) when isAdvanced =>
                    Strength(
                        WorkoutActivityType.HighVolumeStrength,
                        "Advanced physique strength",
                        3,
                        "Barbell Hip Thrust",
                        "Bulgarian Split Squat",
                        "Romanian Deadlift",
                        "Incline Dumbbell Press",
                        "Assisted Pull-Up",
                        "Chest Supported Row",
                        "Cable Lateral Raise",
                        "Pallof Press"),
                (UserGoal.Strength, _) =>
                    Strength(
                        WorkoutActivityType.HighVolumeStrength,
                        "Progressive strength training",
                        3,
                        "Goblet Squat",
                        "Hip Thrust",
                        "Romanian Deadlift",
                        "Incline Dumbbell Press",
                        "Dumbbell Row",
                        "Overhead Press",
                        "Pallof Press"),
                (_, StrengthTrainingStyle.ExpressHard) =>
                    Strength(
                        WorkoutActivityType.StrengthHighIntensity,
                        "Quick strength training",
                        3,
                        isAdvanced
                            ? ["Leg Press", "Cable Glute Kickback", "Chest Supported Row", "Cable Lateral Raise", "Reverse Crunch"]
                            : ["Goblet Squat", "Incline Push-Up", "Dumbbell Row", "Glute Bridge", "Pallof Press"]),
                (_, _) when isAdvanced =>
                    Strength(
                        WorkoutActivityType.HighVolumeStrength,
                        "Advanced physique training",
                        3,
                        "Barbell Hip Thrust",
                        "Leg Press",
                        "Seated Leg Curl",
                        "Cable Glute Kickback",
                        "Chest Supported Row",
                        "Cable Lateral Raise",
                        "Rear Delt Fly",
                        "Cable Woodchop"),
                _ =>
                    Strength(
                        WorkoutActivityType.HighVolumeStrength,
                        "Full body strength training",
                        3,
                        "Goblet Squat",
                        "Incline Push-Up",
                        "Dumbbell Row",
                        "Glute Bridge",
                        "Reverse Lunge",
                        "Pallof Press")
            });
        }

        if (preferences.Contains(WorkoutActivityType.RockClimbing))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.RockClimbing,
                "Climbing pull strength",
                3,
                WorkoutType.Strength,
                "Rock Climbing",
                "Assisted Pull-Up",
                "Seated Cable Row",
                "Lat Pulldown",
                "Face Pull",
                "Biceps Curl",
                "Dead Bug",
                "Pallof Press"));
        }

        if (preferences.Contains(WorkoutActivityType.Yoga))
        {
            templates.Add(TimedActivity(
                WorkoutActivityType.Yoga,
                "Low intensity recovery training",
                1,
                WorkoutType.Recovery,
                "Yoga Flow",
                "Slow Flow Yoga",
                "Hip Opening Yoga",
                "Breathing Reset"));
        }

        if (preferences.Contains(WorkoutActivityType.Hiit))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.Hiit,
                "HIIT conditioning",
                4,
                WorkoutType.Cardio,
                "HIIT Intervals",
                "Glute Bridge",
                "Pallof Press",
                "Plank"));
        }

        if (preferences.Contains(WorkoutActivityType.Cycling))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.Cycling,
                "Cycling conditioning",
                2,
                WorkoutType.Cardio,
                "Cycling Intervals",
                "Mobility Flow",
                "Dead Bug"));
        }

        if (preferences.Contains(WorkoutActivityType.Running))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.Running,
                "Running conditioning",
                3,
                WorkoutType.Cardio,
                "Running Intervals",
                "Mobility Flow",
                "Side Plank"));
        }

        if (preferences.Contains(WorkoutActivityType.Swimming))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.Swimming,
                "Swimming conditioning",
                2,
                WorkoutType.Cardio,
                "Swimming",
                "Mobility Flow",
                "Breathing Reset"));
        }

        return templates;
    }

    private static IReadOnlyList<SessionTemplate> BuildMenstrualPreferredTemplates(
        IReadOnlySet<WorkoutActivityType> preferences)
    {
        var templates = new List<SessionTemplate>();

        if (preferences.Contains(WorkoutActivityType.Yoga))
        {
            templates.Add(TimedActivity(
                WorkoutActivityType.Yoga,
                "Low intensity recovery training",
                1,
                WorkoutType.Recovery,
                "Yoga Flow",
                "Hip Opening Yoga",
                "Breathing Reset",
                "Easy Walk"));
        }

        if (preferences.Contains(WorkoutActivityType.Cycling))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.Cycling,
                "Easy cycling conditioning",
                2,
                WorkoutType.Cardio,
                "Zone 2 Ride",
                "Mobility Flow",
                "Breathing Reset"));
        }

        if (preferences.Contains(WorkoutActivityType.Running))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.Running,
                "Easy running conditioning",
                2,
                WorkoutType.Cardio,
                "Easy Run",
                "Mobility Flow",
                "Breathing Reset"));
        }

        if (preferences.Contains(WorkoutActivityType.Swimming))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.Swimming,
                "Easy swimming conditioning",
                2,
                WorkoutType.Cardio,
                "Swimming",
                "Mobility Flow"));
        }

        if (preferences.Any(activity => activity is WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.StrengthHighIntensity))
        {
            templates.Add(Strength(
                WorkoutActivityType.HighVolumeStrength,
                "Light full body strength",
                2,
                "Glute Bridge",
                "Incline Push-Up",
                "Chest Supported Row",
                "Step-Up",
                "Bird Dog",
                "Pallof Press"));
        }

        if (preferences.Contains(WorkoutActivityType.RockClimbing))
        {
            templates.Add(TimedLeadActivity(
                WorkoutActivityType.RockClimbing,
                "Technique climbing strength",
                2,
                WorkoutType.Strength,
                "Rock Climbing",
                "Face Pull",
                "Dead Bug",
                "Pallof Press"));
        }

        return templates;
    }

    private static IReadOnlyList<SessionTemplate> BuildPhaseTemplates(UserProfile profile, CyclePhase phase) =>
        phase switch
        {
            CyclePhase.Menstrual => BuildMenstrualTemplates(),
            CyclePhase.Luteal => BuildLutealTemplates(profile),
            CyclePhase.Ovulatory => BuildOvulatoryTemplates(profile),
            _ => BuildFollicularTemplates(profile)
        };

    private static IReadOnlyList<WorkoutSlot> BuildWeeklySlots(
        int trainingDays,
        CyclePhase phase,
        PhaseBaseline baseline)
    {
        var lowEnergy = phase is CyclePhase.Menstrual ||
                        baseline.Pain >= 4 ||
                        baseline.Energy is > 0 and <= 2;

        return trainingDays switch
        {
            <= 1 => [Slot(lowEnergy ? WorkoutType.Recovery : WorkoutType.Strength)],
            2 when lowEnergy => [Slot(WorkoutType.Recovery), Slot(WorkoutType.Strength)],
            2 => [Slot(WorkoutType.Strength), Slot(WorkoutType.Strength)],
            3 when lowEnergy => [Slot(WorkoutType.Recovery), Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery)],
            3 => [Slot(WorkoutType.Strength), Slot(WorkoutType.Strength, WorkoutType.Cardio), Slot(WorkoutType.Recovery)],
            4 when lowEnergy => [Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery), Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery)],
            4 => [Slot(WorkoutType.Strength), Slot(WorkoutType.Cardio), Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery)],
            5 when lowEnergy => [Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery), Slot(WorkoutType.Strength), Slot(WorkoutType.Cardio), Slot(WorkoutType.Recovery)],
            5 => [Slot(WorkoutType.Strength), Slot(WorkoutType.Cardio), Slot(WorkoutType.Strength), Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery)],
            _ when lowEnergy => [Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery), Slot(WorkoutType.Strength), Slot(WorkoutType.Cardio), Slot(WorkoutType.Recovery), Slot(WorkoutType.Strength)],
            _ => [Slot(WorkoutType.Strength), Slot(WorkoutType.Cardio), Slot(WorkoutType.Strength), Slot(WorkoutType.Recovery), Slot(WorkoutType.Strength), Slot(WorkoutType.Cardio)]
        };
    }

    private static WorkoutSlot Slot(params WorkoutType[] groups) =>
        new(groups);

    private static SessionTemplate BuildTemplateForSlot(
        WorkoutSlot slot,
        IReadOnlyList<SessionTemplate> candidates,
        IReadOnlyList<SessionTemplate> phaseTemplates,
        IReadOnlyCollection<SessionTemplate> selectedTemplates,
        CyclePhase phase,
        UserGoal goal,
        PhaseBaseline baseline,
        StrengthTrainingStyle strengthStyle)
    {
        var parts = slot.Groups
            .Select(group => PickTemplateForGroup(
                group,
                candidates,
                phaseTemplates,
                selectedTemplates,
                phase,
                goal,
                baseline,
                strengthStyle))
            .ToList();

        return parts.Count == 1
            ? parts[0]
            : CombineTemplates(parts);
    }

    private static SessionTemplate PickTemplateForGroup(
        WorkoutType group,
        IReadOnlyList<SessionTemplate> candidates,
        IReadOnlyList<SessionTemplate> phaseTemplates,
        IReadOnlyCollection<SessionTemplate> selectedTemplates,
        CyclePhase phase,
        UserGoal goal,
        PhaseBaseline baseline,
        StrengthTrainingStyle strengthStyle)
    {
        var usedNames = selectedTemplates
            .Select(template => template.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groupCandidates = candidates
            .Select((template, index) => new { Template = template, Index = index })
            .Where(item => TemplateMatchesGroup(item.Template, group))
            .Where(item => IsTemplateAllowedForPhase(item.Template, phase, baseline))
            .OrderBy(item => usedNames.Contains(item.Template.Name))
            .ThenByDescending(item => ScorePreferredTemplate(item.Template, phase, goal, baseline))
            .ThenBy(item => item.Index)
            .Select(item => item.Template)
            .ToList();

        if (groupCandidates.Count > 0)
            return groupCandidates[0];

        var fallback = phaseTemplates.FirstOrDefault(template => TemplateMatchesGroup(template, group));
        return fallback ?? BuildFallbackTemplate(group, goal, strengthStyle);
    }

    private static bool TemplateMatchesGroup(SessionTemplate template, WorkoutType group) =>
        BuildWorkoutType(template) == group;

    private static bool IsTemplateAllowedForPhase(
        SessionTemplate template,
        CyclePhase phase,
        PhaseBaseline baseline) =>
        template.FatigueScore <= BuildMaxFatigue(phase, baseline);

    private static int BuildMaxFatigue(CyclePhase phase, PhaseBaseline baseline) =>
        phase switch
        {
            CyclePhase.Menstrual => baseline.Energy >= 4 ? 3 : 2,
            CyclePhase.Luteal => baseline.Energy >= 4 ? 3 : 2,
            CyclePhase.Follicular => 4,
            CyclePhase.Ovulatory => 4,
            _ => 3
        };

    private static SessionTemplate CombineTemplates(IReadOnlyList<SessionTemplate> templates)
    {
        var primary = templates[0];
        var primaryGroup = BuildWorkoutType(primary);
        var exercises = new List<string>(BuildPrimaryExerciseList(primary, primaryGroup));
        var timedNames = new HashSet<string>(BuildTimedNames(primary), StringComparer.OrdinalIgnoreCase);

        foreach (var secondary in templates.Skip(1))
        {
            foreach (var exerciseName in BuildSecondaryExerciseList(secondary))
                exercises.Add(exerciseName);

            foreach (var exerciseName in BuildTimedNames(secondary))
                timedNames.Add(exerciseName);
        }

        var distinctExercises = exercises
            .Where(exerciseName => !string.IsNullOrWhiteSpace(exerciseName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SessionTemplate(
            BuildCombinedTemplateName(templates),
            false,
            distinctExercises,
            primary.ActivityType,
            primaryGroup,
            Math.Min(4, templates.Sum(template => template.FatigueScore)),
            timedNames.ToList());
    }

    private static IReadOnlyList<string> BuildPrimaryExerciseList(
        SessionTemplate template,
        WorkoutType primaryGroup) =>
        primaryGroup is WorkoutType.Strength
            ? template.ExerciseNames.Take(6).ToList()
            : template.ExerciseNames.ToList();

    private static IReadOnlyList<string> BuildSecondaryExerciseList(SessionTemplate template)
    {
        var group = BuildWorkoutType(template);
        if (group is WorkoutType.Cardio)
            return template.TimedExerciseNames.Count > 0
                ? template.TimedExerciseNames.Take(1).ToList()
                : template.ExerciseNames.Take(1).ToList();

        if (group is WorkoutType.Recovery)
            return template.ExerciseNames.Take(2).ToList();

        return template.ExerciseNames.Take(4).ToList();
    }

    private static IEnumerable<string> BuildTimedNames(SessionTemplate template)
    {
        if (template.IsTimed)
            return template.ExerciseNames;

        return template.TimedExerciseNames;
    }

    private static string BuildCombinedTemplateName(IReadOnlyList<SessionTemplate> templates)
    {
        var groups = templates.Select(BuildWorkoutType).ToHashSet();
        if (groups.Contains(WorkoutType.Strength) && groups.Contains(WorkoutType.Cardio))
            return templates[0].ActivityType is WorkoutActivityType.RockClimbing
                ? "Climbing and conditioning training"
                : "Strength and conditioning training";

        if (groups.Contains(WorkoutType.Strength) && groups.Contains(WorkoutType.Recovery))
            return "Strength and recovery training";

        if (groups.Contains(WorkoutType.Cardio) && groups.Contains(WorkoutType.Recovery))
            return "Cardio and recovery training";

        return templates[0].Name;
    }

    private static int ScorePreferredTemplate(
        SessionTemplate template,
        CyclePhase phase,
        UserGoal goal,
        PhaseBaseline baseline)
    {
        var score = goal switch
        {
            UserGoal.Strength when IsStrengthActivity(template.ActivityType) => 6,
            UserGoal.FatLoss when template.ActivityType is WorkoutActivityType.Hiit or WorkoutActivityType.Running => 7,
            UserGoal.FatLoss when IsCardioActivity(template.ActivityType) => 5,
            UserGoal.MuscleTone when IsStrengthActivity(template.ActivityType) => 4,
            _ => 2
        };

        score += phase switch
        {
            CyclePhase.Menstrual => ScoreMenstrualPreference(template, baseline),
            CyclePhase.Follicular => template.ActivityType switch
            {
                WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.RockClimbing => 5,
                WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.Hiit or WorkoutActivityType.Running => 4,
                WorkoutActivityType.Cycling or WorkoutActivityType.Swimming or WorkoutActivityType.Yoga => 3,
                _ => 1
            },
            CyclePhase.Ovulatory => template.ActivityType switch
            {
                WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.RockClimbing or WorkoutActivityType.Hiit => 6,
                WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.Running => 5,
                WorkoutActivityType.Cycling or WorkoutActivityType.Swimming => 4,
                WorkoutActivityType.Yoga => 2,
                _ => 1
            },
            CyclePhase.Luteal => template.ActivityType switch
            {
                WorkoutActivityType.Yoga => 6,
                WorkoutActivityType.Cycling or WorkoutActivityType.Swimming or WorkoutActivityType.Running => 4,
                WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.RockClimbing => 3,
                WorkoutActivityType.StrengthHighIntensity or WorkoutActivityType.Hiit => -3,
                _ => 1
            },
            _ => 0
        };

        return score - template.FatigueScore;
    }

    private static int ScoreMenstrualPreference(SessionTemplate template, PhaseBaseline baseline)
    {
        if (baseline.Energy < 4)
        {
            return template.ActivityType switch
            {
                WorkoutActivityType.Yoga => 7,
                WorkoutActivityType.Cycling or WorkoutActivityType.Swimming or WorkoutActivityType.Running => 3,
                WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.RockClimbing => 1,
                _ => -6
            };
        }

        return template.ActivityType switch
        {
            WorkoutActivityType.Yoga => 5,
            WorkoutActivityType.Cycling or WorkoutActivityType.Swimming or WorkoutActivityType.Running => 3,
            WorkoutActivityType.HighVolumeStrength or WorkoutActivityType.RockClimbing => 2,
            _ => -4
        };
    }

    private static bool IsStrengthActivity(WorkoutActivityType activityType) =>
        activityType is WorkoutActivityType.StrengthHighIntensity or
            WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.RockClimbing;

    private static bool IsCardioActivity(WorkoutActivityType activityType) =>
        activityType is WorkoutActivityType.Hiit or
            WorkoutActivityType.Cycling or
            WorkoutActivityType.Running or
            WorkoutActivityType.Swimming;

    private static bool IsAdvancedPhysiqueProfile(UserProfile profile) =>
        profile.TrainingExperienceLevel is TrainingExperienceLevel.Advanced &&
        profile.Goal is UserGoal.Strength or UserGoal.MuscleTone or UserGoal.FatLoss;

    private static IReadOnlyList<SessionTemplate> BuildMenstrualTemplates() =>
    [
        Timed("Low intensity recovery training", "Mobility Flow", "Hip Opening Yoga", "Easy Walk", "Breathing Reset"),
        Strength(WorkoutActivityType.HighVolumeStrength, "Light full body strength", 2, "Glute Bridge", "Incline Push-Up", "Chest Supported Row", "Step-Up", "Bird Dog", "Pallof Press"),
        Timed("Gentle yoga and walk", "Yoga Flow", "Hip Opening Yoga", "Breathing Reset", "Easy Walk"),
        TimedLeadActivity(WorkoutActivityType.Cycling, "Easy cycling conditioning", 2, WorkoutType.Cardio, "Zone 2 Ride", "Mobility Flow", "Breathing Reset"),
        Strength(WorkoutActivityType.HighVolumeStrength, "Light lower body training", 2, "Step-Up", "Glute Bridge", "Cable Glute Kickback", "Calf Raise", "Pallof Press")
    ];

    private static IReadOnlyList<SessionTemplate> BuildFollicularTemplates(UserProfile profile)
    {
        var goal = profile.Goal;
        if (IsAdvancedPhysiqueProfile(profile))
        {
            return
            [
                Strength(WorkoutActivityType.HighVolumeStrength, "Advanced lower body strength", 3, "Barbell Hip Thrust", "Bulgarian Split Squat", "Romanian Deadlift", "Leg Press", "Seated Leg Curl", "Cable Hip Abduction", "Pallof Press"),
                Strength(WorkoutActivityType.HighVolumeStrength, "Glutes and hamstrings", 3, "Barbell Hip Thrust", "Single-Leg Romanian Deadlift", "Cable Glute Kickback", "Seated Leg Curl", "Back Extension", "Cable Hip Abduction", "Side Plank"),
                Strength(WorkoutActivityType.HighVolumeStrength, "Upper pull and shoulders", 3, "Assisted Pull-Up", "Lat Pulldown", "Single-Arm Cable Row", "Cable Lateral Raise", "Rear Delt Fly", "Biceps Curl", "Triceps Pressdown", "Cable Woodchop"),
                Strength(WorkoutActivityType.HighVolumeStrength, "Upper shape strength", 3, "Assisted Pull-Up", "Chest Supported Row", "Incline Dumbbell Press", "Overhead Press", "Cable Lateral Raise", "Rear Delt Fly", "Face Pull"),
                goal is UserGoal.FatLoss
                    ? TimedLeadActivity(WorkoutActivityType.Hiit, "HIIT conditioning", 4, WorkoutType.Cardio, "HIIT Intervals", "Leg Press", "Cable Woodchop", "Plank")
                    : Strength(WorkoutActivityType.HighVolumeStrength, "Full body physique strength", 3, "Leg Press", "Incline Dumbbell Press", "Single-Arm Cable Row", "Cable Pull-Through", "Walking Lunge", "Cable Lateral Raise", "Cable Woodchop"),
                TimedLeadActivity(WorkoutActivityType.Cycling, "Cycling conditioning", 2, WorkoutType.Cardio, "Cycling Intervals", "Mobility Flow", "Hip Opening Yoga", "Reverse Crunch"),
                Timed("Low intensity recovery training", "Yoga Flow", "Slow Flow Yoga", "Hip Opening Yoga", "Breathing Reset")
            ];
        }

        var templates = new List<SessionTemplate>
        {
            Strength(WorkoutActivityType.HighVolumeStrength, "Lower body strength", 3, "Goblet Squat", "Hip Thrust", "Romanian Deadlift", "Step-Up", "Reverse Lunge", "Calf Raise", "Pallof Press"),
            Strength(WorkoutActivityType.HighVolumeStrength, "Upper body strength", 3, "Incline Push-Up", "Incline Dumbbell Press", "Dumbbell Row", "Overhead Press", "Lat Pulldown", "Lateral Raise", "Face Pull"),
            Strength(WorkoutActivityType.HighVolumeStrength, "Glutes and core", 3, "Hip Thrust", "Reverse Lunge", "Romanian Deadlift", "Step-Up", "Dead Bug", "Side Plank", "Pallof Press")
        };

        if (goal is UserGoal.FatLoss)
            templates.Add(TimedLeadActivity(WorkoutActivityType.Hiit, "HIIT conditioning", 4, WorkoutType.Cardio, "HIIT Intervals", "Glute Bridge", "Pallof Press", "Plank"));
        else
            templates.Add(Strength(WorkoutActivityType.HighVolumeStrength, "Full body strength", 3, "Goblet Squat", "Incline Dumbbell Press", "Romanian Deadlift", "Seated Cable Row", "Reverse Lunge", "Glute Bridge", "Pallof Press"));

        templates.Add(Strength(WorkoutActivityType.HighVolumeStrength, "Upper pull and arms", 3, "Seated Cable Row", "Lat Pulldown", "Lateral Raise", "Biceps Curl", "Triceps Pressdown", "Face Pull", "Dead Bug"));
        templates.Add(TimedLeadActivity(WorkoutActivityType.Cycling, "Cycling conditioning", 2, WorkoutType.Cardio, "Cycling Intervals", "Mobility Flow", "Plank"));
        templates.Add(Timed("Low intensity recovery training", "Yoga Flow", "Slow Flow Yoga", "Hip Opening Yoga", "Breathing Reset"));
        return templates;
    }

    private static IReadOnlyList<SessionTemplate> BuildOvulatoryTemplates(UserProfile profile)
    {
        var goal = profile.Goal;
        if (IsAdvancedPhysiqueProfile(profile))
        {
            var conditioningName = goal is UserGoal.FatLoss ? "HIIT conditioning" : "Power conditioning";
            return
            [
                Strength(WorkoutActivityType.StrengthHighIntensity, "Peak lower body strength", 4, "Barbell Hip Thrust", "Leg Press", "Bulgarian Split Squat", "Romanian Deadlift", "Seated Leg Curl", "Cable Hip Abduction", "Pallof Press"),
                Strength(WorkoutActivityType.StrengthHighIntensity, "Peak upper body strength", 4, "Assisted Pull-Up", "Chest Supported Row", "Overhead Press", "Incline Dumbbell Press", "Cable Lateral Raise", "Rear Delt Fly", "Face Pull"),
                TimedLeadActivity(WorkoutActivityType.Hiit, conditioningName, 4, WorkoutType.Cardio, "HIIT Intervals", "Walking Lunge", "Cable Woodchop", "Hanging Knee Raise"),
                Strength(WorkoutActivityType.HighVolumeStrength, "Glute volume training", 3, "Barbell Hip Thrust", "Cable Glute Kickback", "Single-Leg Romanian Deadlift", "Back Extension", "Leg Extension", "Copenhagen Side Plank", "Reverse Crunch"),
                Strength(WorkoutActivityType.HighVolumeStrength, "Back and shoulder detail", 3, "Assisted Pull-Up", "Lat Pulldown", "Single-Arm Cable Row", "Cable Lateral Raise", "Rear Delt Fly", "Biceps Curl", "Triceps Pressdown"),
                TimedActivity(WorkoutActivityType.Yoga, "Yoga strength recovery", 2, WorkoutType.Recovery, "Power Yoga", "Vinyasa Flow", "Mobility Flow", "Side Plank")
            ];
        }

        var templates = new List<SessionTemplate>
        {
            Strength(WorkoutActivityType.StrengthHighIntensity, "Heavy lower body strength", 4, "Goblet Squat", "Hip Thrust", "Romanian Deadlift", "Reverse Lunge", "Step-Up", "Calf Raise", "Pallof Press"),
            Strength(WorkoutActivityType.StrengthHighIntensity, "Heavy upper body strength", 4, "Incline Dumbbell Press", "Dumbbell Row", "Overhead Press", "Seated Cable Row", "Lat Pulldown", "Lateral Raise", "Face Pull"),
            TimedLeadActivity(WorkoutActivityType.Hiit, goal is UserGoal.FatLoss ? "HIIT conditioning" : "Power conditioning", 4, WorkoutType.Cardio, "HIIT Intervals", "Glute Bridge", "Plank", "Pallof Press"),
            Strength(WorkoutActivityType.HighVolumeStrength, "Glute strength", 3, "Hip Thrust", "Step-Up", "Romanian Deadlift", "Reverse Lunge", "Side Plank", "Pallof Press")
        };

        templates.Add(Strength(WorkoutActivityType.HighVolumeStrength, "Upper pull and arms", 3, "Seated Cable Row", "Lat Pulldown", "Dumbbell Row", "Lateral Raise", "Biceps Curl", "Triceps Pressdown", "Face Pull"));
        templates.Add(TimedLeadActivity(WorkoutActivityType.Cycling, "Cycling conditioning", 2, WorkoutType.Cardio, "Cycling Intervals", "Mobility Flow", "Dead Bug"));
        templates.Add(TimedActivity(WorkoutActivityType.Yoga, "Yoga strength recovery", 2, WorkoutType.Recovery, "Power Yoga", "Vinyasa Flow", "Mobility Flow", "Side Plank"));
        return templates;
    }

    private static IReadOnlyList<SessionTemplate> BuildLutealTemplates(UserProfile profile)
    {
        var goal = profile.Goal;
        if (IsAdvancedPhysiqueProfile(profile))
        {
            return
            [
                Strength(WorkoutActivityType.HighVolumeStrength, "Controlled lower body strength", 3, "Leg Press", "Glute Bridge", "Seated Leg Curl", "Cable Glute Kickback", "Cable Hip Abduction", "Reverse Crunch", "Pallof Press"),
                Strength(WorkoutActivityType.HighVolumeStrength, "Controlled upper body strength", 3, "Chest Supported Row", "Lat Pulldown", "Incline Push-Up", "Cable Lateral Raise", "Rear Delt Fly", "Face Pull", "Side Plank"),
                TimedLeadActivity(WorkoutActivityType.Cycling, "Cycling conditioning", 2, WorkoutType.Cardio, "Zone 2 Ride", "Mobility Flow", "Hip Opening Yoga", "Breathing Reset"),
                Strength(WorkoutActivityType.HighVolumeStrength, goal is UserGoal.Strength ? "Technique strength training" : "Controlled full body training", 2, "Bulgarian Split Squat", "Single-Arm Cable Row", "Cable Pull-Through", "Overhead Press", "Cable Woodchop", "Bird Dog"),
                Timed("Low intensity recovery training", "Slow Flow Yoga", "Hip Opening Yoga", "Easy Walk", "Breathing Reset")
            ];
        }

        var templates = new List<SessionTemplate>
        {
            Strength(WorkoutActivityType.HighVolumeStrength, "Controlled lower body strength", 2, "Goblet Squat", "Glute Bridge", "Step-Up", "Reverse Lunge", "Dead Bug", "Pallof Press"),
            Strength(WorkoutActivityType.HighVolumeStrength, "Controlled upper body strength", 2, "Incline Push-Up", "Dumbbell Row", "Seated Cable Row", "Lateral Raise", "Face Pull", "Side Plank"),
            TimedLeadActivity(WorkoutActivityType.Cycling, "Cycling conditioning", 2, WorkoutType.Cardio, "Zone 2 Ride", "Mobility Flow", "Side Plank"),
            Strength(WorkoutActivityType.HighVolumeStrength, goal is UserGoal.Strength ? "Technique strength training" : "Controlled full body training", 2, "Reverse Lunge", "Seated Cable Row", "Overhead Press", "Glute Bridge", "Pallof Press", "Bird Dog"),
            Timed("Low intensity recovery training", "Yoga Flow", "Easy Walk", "Bird Dog")
        };

        return templates;
    }

    private static SessionTemplate BuildFallbackTemplate(
        WorkoutType group,
        UserGoal goal,
        StrengthTrainingStyle strengthStyle) =>
        group switch
        {
            WorkoutType.Cardio => TimedLeadActivity(WorkoutActivityType.Cycling, "Cycling conditioning", 2, WorkoutType.Cardio, "Cycling Intervals", "Mobility Flow"),
            WorkoutType.Recovery => Timed("Low intensity recovery training", "Yoga Flow", "Hip Opening Yoga", "Breathing Reset"),
            _ => Strength(
                strengthStyle is StrengthTrainingStyle.ExpressHard ? WorkoutActivityType.StrengthHighIntensity : WorkoutActivityType.HighVolumeStrength,
                goal is UserGoal.Strength ? "Progressive strength training" : "Full body strength training",
                strengthStyle is StrengthTrainingStyle.ExpressHard ? 4 : 3,
                "Goblet Squat",
                "Hip Thrust",
                "Romanian Deadlift",
                "Incline Dumbbell Press",
                "Dumbbell Row",
                "Pallof Press")
        };

    private static SessionTemplate Strength(
        WorkoutActivityType activityType,
        string name,
        int fatigueScore,
        params string[] exerciseNames) =>
        new(name, false, exerciseNames, activityType, null, fatigueScore, []);

    private static SessionTemplate Timed(string name, params string[] exerciseNames) =>
        new(name, true, exerciseNames, WorkoutActivityType.Yoga, WorkoutType.Recovery, 1, []);

    private static SessionTemplate TimedActivity(
        WorkoutActivityType activityType,
        string name,
        int fatigueScore,
        WorkoutType? workoutTypeOverride,
        params string[] exerciseNames) =>
        new(name, true, exerciseNames, activityType, workoutTypeOverride, fatigueScore, []);

    private static SessionTemplate TimedLeadActivity(
        WorkoutActivityType activityType,
        string name,
        int fatigueScore,
        WorkoutType? workoutTypeOverride,
        string timedExerciseName,
        params string[] accessoryExerciseNames)
    {
        var exercises = new[] { timedExerciseName }
            .Concat(accessoryExerciseNames)
            .ToList();
        return new(name, false, exercises, activityType, workoutTypeOverride, fatigueScore, [timedExerciseName]);
    }

    private sealed record WorkoutSlot(IReadOnlyList<WorkoutType> Groups);

    private sealed record SessionTemplate(
        string Name,
        bool IsTimed,
        IReadOnlyList<string> ExerciseNames,
        WorkoutActivityType ActivityType,
        WorkoutType? WorkoutTypeOverride,
        int FatigueScore,
        IReadOnlyList<string> TimedExerciseNames);
}
