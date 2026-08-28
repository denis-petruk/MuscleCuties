using System.Text;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    private async Task SeedStarterExercisesAsync()
    {
        await BackfillExerciseCodesAsync();

        var existingNames = await Exercises
            .Select(exercise => exercise.Name)
            .ToListAsync();
        var existingSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exercises = BuildStarterExercises()
            .Where(exercise => !existingSet.Contains(exercise.Name))
            .ToList();

        if (exercises.Count == 0)
            return;

        await Exercises.AddRangeAsync(exercises);
        await SaveChangesAsync();
    }

    private async Task BackfillExerciseCodesAsync()
    {
        var exercises = await Exercises.ToListAsync();
        var usedCodes = exercises
            .Select(exercise => exercise.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var exercise in exercises.Where(exercise => string.IsNullOrWhiteSpace(exercise.Code)))
        {
            exercise.Code = BuildUniqueExerciseCode(exercise.Name, usedCodes);
            changed = true;
        }

        if (changed)
            await SaveChangesAsync();
    }

    private static List<Exercise> BuildStarterExercises() =>
    [
        Exercise("Goblet Squat", "A controlled squat pattern for quads, glutes, and full-body bracing.", MuscleGroup.Quads, "Glutes,Hamstrings"),
        Exercise("Hip Thrust", "A hip-extension strength movement focused on glutes.", MuscleGroup.Glutes, "Hamstrings,Abs"),
        Exercise("Barbell Hip Thrust", "A heavy hip-extension movement for progressive glute strength.", MuscleGroup.Glutes, "Hamstrings,Abs"),
        Exercise("Romanian Deadlift", "A hip-hinge movement for hamstrings, glutes, and posterior-chain control.", MuscleGroup.Hamstrings, "Glutes,LowerBack", "LowerBack"),
        Exercise("Single-Leg Romanian Deadlift", "A single-leg hinge for hamstrings, glutes, balance, and hip control.", MuscleGroup.Hamstrings, "Glutes,LowerBack", "LowerBack,Hip"),
        Exercise("Cable Pull-Through", "A cable hinge pattern that builds glutes and hamstrings with lower spinal demand.", MuscleGroup.Glutes, "Hamstrings,LowerBack"),
        Exercise("Step-Up", "A single-leg lower-body exercise for glutes, quads, and balance.", MuscleGroup.Glutes, "Quads,Calves"),
        Exercise("Reverse Lunge", "A single-leg strength movement with controlled knee and hip loading.", MuscleGroup.Quads, "Glutes,Hamstrings", "Knee"),
        Exercise("Bulgarian Split Squat", "A high-return single-leg squat for glutes, quads, and balance.", MuscleGroup.Glutes, "Quads,Hamstrings", "Knee,Hip"),
        Exercise("Walking Lunge", "A dynamic single-leg pattern for glutes, quads, conditioning, and coordination.", MuscleGroup.Quads, "Glutes,Hamstrings", "Knee"),
        Exercise("Leg Press", "A stable lower-body strength movement that lets quads and glutes take more volume.", MuscleGroup.Quads, "Glutes,Hamstrings", "Knee"),
        Exercise("Leg Extension", "A focused quad accessory for extra thigh volume without taxing the whole body.", MuscleGroup.Quads, string.Empty, "Knee"),
        Exercise("Seated Leg Curl", "A hamstring isolation movement for knee-flexion strength and posterior-leg balance.", MuscleGroup.Hamstrings, string.Empty, "Knee"),
        Exercise("Glute Bridge", "A low-impact glute exercise that works well for lighter cycle phases.", MuscleGroup.Glutes, "Hamstrings,Abs"),
        Exercise("Cable Glute Kickback", "A glute accessory that trains hip extension with a long squeeze and low systemic fatigue.", MuscleGroup.Glutes, "Hamstrings"),
        Exercise("Cable Hip Abduction", "A glute medius accessory for hip shape, pelvic control, and single-leg stability.", MuscleGroup.Glutes, "HipFlexors"),
        Exercise("Back Extension", "A posterior-chain accessory that can bias glutes, hamstrings, or lower back by setup.", MuscleGroup.Glutes, "Hamstrings,LowerBack", "LowerBack"),
        Exercise("Calf Raise", "A simple calf-strength movement for lower-leg support.", MuscleGroup.Calves, string.Empty),
        Exercise("Incline Push-Up", "A scalable upper-body push pattern for chest, shoulders, and triceps.", MuscleGroup.Chest, "FrontShoulders,Triceps", "Wrist,Shoulder"),
        Exercise("Incline Dumbbell Press", "A chest and shoulder press that supports upper-body strength progression.", MuscleGroup.Chest, "FrontShoulders,Triceps", "Shoulder"),
        Exercise("Dumbbell Row", "A back-strength exercise for lats, upper back, and shoulder support.", MuscleGroup.UpperBack, "Biceps,RearShoulders"),
        Exercise("Chest Supported Row", "A strict horizontal pull for upper-back thickness without lower-back cheating.", MuscleGroup.UpperBack, "Biceps,RearShoulders"),
        Exercise("Single-Arm Cable Row", "A unilateral cable row for lats, upper back, and balanced pulling strength.", MuscleGroup.UpperBack, "Biceps,RearShoulders"),
        Exercise("Seated Cable Row", "A controlled horizontal pull for upper-back strength.", MuscleGroup.UpperBack, "Biceps,RearShoulders"),
        Exercise("Lat Pulldown", "A vertical pulling exercise for back strength and posture support.", MuscleGroup.UpperBack, "Biceps,RearShoulders"),
        Exercise("Assisted Pull-Up", "A vertical pull progression for back width, biceps, grip, and torso control.", MuscleGroup.UpperBack, "Biceps,Forearms"),
        Exercise("Overhead Press", "A shoulder press pattern for upper-body strength.", MuscleGroup.FrontShoulders, "SideShoulders,Triceps", "Shoulder"),
        Exercise("Lateral Raise", "A shoulder accessory movement for side delts.", MuscleGroup.SideShoulders, string.Empty, "Shoulder"),
        Exercise("Cable Lateral Raise", "A constant-tension side-delt accessory for shoulder shape.", MuscleGroup.SideShoulders, string.Empty, "Shoulder"),
        Exercise("Rear Delt Fly", "A rear-shoulder accessory for posture, shoulder balance, and upper-back detail.", MuscleGroup.RearShoulders, "UpperBack", "Shoulder"),
        Exercise("Face Pull", "A rear-shoulder and upper-back accessory for posture and shoulder balance.", MuscleGroup.RearShoulders, "UpperBack"),
        Exercise("Biceps Curl", "A simple arm accessory movement.", MuscleGroup.Biceps, "Forearms", "Elbow"),
        Exercise("Triceps Pressdown", "A simple arm accessory movement for triceps.", MuscleGroup.Triceps, string.Empty, "Elbow"),
        Exercise("Dead Bug", "A low-impact core stability drill.", MuscleGroup.Abs, "HipFlexors"),
        Exercise("Side Plank", "A lateral core stability exercise.", MuscleGroup.Obliques, "Abs,SideShoulders", "Shoulder"),
        Exercise("Copenhagen Side Plank", "An advanced side-plank variation for adductors, obliques, and hip stability.", MuscleGroup.Adductors, "Obliques,Abs", "Hip"),
        Exercise("Pallof Press", "An anti-rotation core exercise for trunk stability.", MuscleGroup.Obliques, "Abs"),
        Exercise("Cable Woodchop", "A rotational core drill for obliques, rib control, and athletic trunk strength.", MuscleGroup.Obliques, "Abs"),
        Exercise("Bird Dog", "A low-impact core and hip stability drill.", MuscleGroup.Abs, "Glutes,LowerBack"),
        Exercise("Plank", "A core endurance exercise for trunk stiffness and shoulder support.", MuscleGroup.Abs, "FrontShoulders", "Shoulder"),
        Exercise("Hanging Knee Raise", "A hip-flexion and lower-ab drill for advanced trunk control.", MuscleGroup.Abs, "HipFlexors,Forearms", "Shoulder"),
        Exercise("Reverse Crunch", "A controlled lower-ab pattern that trains pelvis position without spinal loading.", MuscleGroup.Abs, "HipFlexors"),
        Exercise("Bike Intervals", "Low-impact conditioning intervals for cardio capacity.", MuscleGroup.Quads, "Glutes,Calves"),
        Exercise("HIIT Intervals", "Short hard conditioning blocks with enough recovery to keep each interval powerful.", MuscleGroup.Quads, "Glutes,Calves,Abs", "Knee"),
        Exercise("Cycling Intervals", "Bike-based conditioning for stronger legs and repeatable aerobic power.", MuscleGroup.Quads, "Glutes,Calves"),
        Exercise("Running Intervals", "Run-based conditioning built around pace, heart rate, and clean repeat efforts.", MuscleGroup.Calves, "Glutes,Quads,Hamstrings", "Knee"),
        Exercise("Tempo Run", "A controlled faster run for aerobic capacity without turning every minute into a sprint.", MuscleGroup.Calves, "Glutes,Quads,Hamstrings", "Knee"),
        Exercise("Easy Run", "A low-stress jog for easy aerobic work and recovery-friendly movement.", MuscleGroup.Calves, "Glutes,Quads,Hamstrings", "Knee"),
        Exercise("Zone 2 Ride", "Steady low-impact cardio for endurance and recovery.", MuscleGroup.Quads, "Glutes,Calves"),
        Exercise("Easy Walk", "Gentle low-impact movement for recovery days.", MuscleGroup.Calves, "Glutes,Quads"),
        Exercise("Mobility Flow", "A full-body mobility sequence for recovery and joint range.", MuscleGroup.HipFlexors, "Abs,Glutes"),
        Exercise("Yoga Flow", "A recovery-focused flow for breath, mobility, and light strength.", MuscleGroup.Abs, "Glutes,HipFlexors"),
        Exercise("Slow Flow Yoga", "A longer steady yoga practice for breath, positions, and calm strength.", MuscleGroup.Abs, "Glutes,HipFlexors"),
        Exercise("Hip Opening Yoga", "A lower-body yoga sequence for hips, glutes, adductors, and relaxed range.", MuscleGroup.HipFlexors, "Glutes,Adductors"),
        Exercise("Vinyasa Flow", "A continuous yoga flow for heat, balance, breath, and controlled transitions.", MuscleGroup.Abs, "Glutes,FrontShoulders", "Shoulder,Wrist"),
        Exercise("Power Yoga", "A stronger yoga practice for heat, balance, and control.", MuscleGroup.Abs, "Glutes,FrontShoulders", "Shoulder,Wrist"),
        Exercise("Yin Yoga", "Slow supported holds for hips, back, and nervous-system downshift.", MuscleGroup.HipFlexors, "Glutes,LowerBack"),
        Exercise("Restorative Yoga", "A gentle recovery practice built around breath and supported positions.", MuscleGroup.Abs, "HipFlexors"),
        Exercise("Pilates Flow", "A core-control session with smooth transitions and steady breathing.", MuscleGroup.Abs, "Obliques,HipFlexors"),
        Exercise("Active Recovery Flow", "Light movement for circulation, mobility, and better recovery.", MuscleGroup.HipFlexors, "Abs,Glutes"),
        Exercise("Breathing Reset", "A downshift drill for calm breathing and relaxed bracing.", MuscleGroup.Abs, string.Empty),
        Exercise("Rock Climbing", "A skill-based climbing session for grip, pulling strength, and body tension.", MuscleGroup.UpperBack, "Biceps,Forearms,Abs", "Shoulder,Wrist,Elbow"),
        Exercise("Swimming", "Low-impact cardio for smooth conditioning and joint-friendly volume.", MuscleGroup.UpperBack, "Chest,Glutes,Quads"),
        Exercise("Dance Cardio", "Rhythmic conditioning that builds coordination and aerobic work capacity.", MuscleGroup.Calves, "Glutes,Quads,Abs")
    ];

    private static Exercise Exercise(
        string name,
        string description,
        MuscleGroup primaryMuscle,
        string secondaryMuscles,
        string jointAreas = "") =>
        new()
        {
            Code = BuildExerciseCode(name),
            Name = name,
            Description = description,
            PrimaryMuscle = primaryMuscle,
            SecondaryMuscles = secondaryMuscles,
            JointAreas = jointAreas,
            IsInjuryFriendly = !HasJointStress(jointAreas)
        };

    private static bool HasJointStress(string jointAreas) =>
        !string.IsNullOrWhiteSpace(jointAreas);

    private static string BuildUniqueExerciseCode(string name, ISet<string> usedCodes)
    {
        var code = BuildExerciseCode(name);
        var uniqueCode = code;
        var suffix = 2;

        while (!usedCodes.Add(uniqueCode))
        {
            uniqueCode = $"{code}_{suffix}";
            suffix++;
        }

        return uniqueCode;
    }

    private static string BuildExerciseCode(string name)
    {
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in name.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        var code = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(code) ? "EXERCISE" : code;
    }
}
