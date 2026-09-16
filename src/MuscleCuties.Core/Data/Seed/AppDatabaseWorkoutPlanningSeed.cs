using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    internal async Task SeedWorkoutPlanningReferenceDataAsync()
    {
        await SeedWorkoutMuscleGroupsAsync();
        await SeedGoalTierWeightsAsync();
        await SeedVolumeBudgetRowsAsync();
        await SeedWorkoutExerciseDefinitionsAsync();
        await SeedExerciseMuscleContributionsAsync();
        await SeedSessionArchetypesAsync();
        await SeedSlotTemplatesAsync();
        await SeedWeekTemplatesAsync();
    }

    private async Task SeedWorkoutMuscleGroupsAsync()
    {
        if (await WorkoutMuscleGroups.AnyAsync())
            return;

        WorkoutMuscleGroups.AddRange(
            new WorkoutMuscleGroup { Id = 1, Name = "Glutes" },
            new WorkoutMuscleGroup { Id = 2, Name = "Hamstrings" },
            new WorkoutMuscleGroup { Id = 3, Name = "Abductors" },
            new WorkoutMuscleGroup { Id = 4, Name = "Adductors" },
            new WorkoutMuscleGroup { Id = 5, Name = "Quads" },
            new WorkoutMuscleGroup { Id = 6, Name = "Lats" },
            new WorkoutMuscleGroup { Id = 7, Name = "UpperBack" },
            new WorkoutMuscleGroup { Id = 8, Name = "Delts" },
            new WorkoutMuscleGroup { Id = 9, Name = "Chest" },
            new WorkoutMuscleGroup { Id = 10, Name = "Biceps" },
            new WorkoutMuscleGroup { Id = 11, Name = "Triceps" },
            new WorkoutMuscleGroup { Id = 12, Name = "Core" },
            new WorkoutMuscleGroup { Id = 13, Name = "Calves" });

        await SaveChangesAsync();
    }

    private async Task SeedGoalTierWeightsAsync()
    {
        if (await GoalTierWeights.AnyAsync())
            return;

        var rows = new List<GoalTierWeight>();
        int id = 1;

        void Add(UserGoal goal, int muscleId, PriorityTier tier)
            => rows.Add(new GoalTierWeight { Id = id++, Goal = goal, MuscleGroupId = muscleId, Tier = tier });

        // MuscleTone
        Add(UserGoal.MuscleTone, 1, PriorityTier.A);   // Glutes
        Add(UserGoal.MuscleTone, 3, PriorityTier.A);   // Abductors
        Add(UserGoal.MuscleTone, 2, PriorityTier.A);   // Hamstrings
        Add(UserGoal.MuscleTone, 6, PriorityTier.B);   // Lats
        Add(UserGoal.MuscleTone, 7, PriorityTier.B);   // UpperBack
        Add(UserGoal.MuscleTone, 12, PriorityTier.B);  // Core
        Add(UserGoal.MuscleTone, 8, PriorityTier.B);   // Delts
        Add(UserGoal.MuscleTone, 10, PriorityTier.B);  // Biceps
        Add(UserGoal.MuscleTone, 11, PriorityTier.B);  // Triceps
        Add(UserGoal.MuscleTone, 5, PriorityTier.C);   // Quads
        Add(UserGoal.MuscleTone, 9, PriorityTier.C);   // Chest
        Add(UserGoal.MuscleTone, 4, PriorityTier.C);   // Adductors
        Add(UserGoal.MuscleTone, 13, PriorityTier.C);  // Calves

        // Strength
        Add(UserGoal.Strength, 1, PriorityTier.A);
        Add(UserGoal.Strength, 3, PriorityTier.B);
        Add(UserGoal.Strength, 2, PriorityTier.A);
        Add(UserGoal.Strength, 6, PriorityTier.A);
        Add(UserGoal.Strength, 7, PriorityTier.A);
        Add(UserGoal.Strength, 12, PriorityTier.B);
        Add(UserGoal.Strength, 8, PriorityTier.B);
        Add(UserGoal.Strength, 10, PriorityTier.C);
        Add(UserGoal.Strength, 11, PriorityTier.C);
        Add(UserGoal.Strength, 5, PriorityTier.A);
        Add(UserGoal.Strength, 9, PriorityTier.A);
        Add(UserGoal.Strength, 4, PriorityTier.C);
        Add(UserGoal.Strength, 13, PriorityTier.C);

        // FatLoss
        Add(UserGoal.FatLoss, 1, PriorityTier.A);
        Add(UserGoal.FatLoss, 3, PriorityTier.B);
        Add(UserGoal.FatLoss, 2, PriorityTier.A);
        Add(UserGoal.FatLoss, 6, PriorityTier.B);
        Add(UserGoal.FatLoss, 7, PriorityTier.B);
        Add(UserGoal.FatLoss, 12, PriorityTier.B);
        Add(UserGoal.FatLoss, 8, PriorityTier.C);
        Add(UserGoal.FatLoss, 10, PriorityTier.C);
        Add(UserGoal.FatLoss, 11, PriorityTier.C);
        Add(UserGoal.FatLoss, 5, PriorityTier.B);
        Add(UserGoal.FatLoss, 9, PriorityTier.C);
        Add(UserGoal.FatLoss, 4, PriorityTier.C);
        Add(UserGoal.FatLoss, 13, PriorityTier.C);

        // MaintainHealth
        Add(UserGoal.MaintainHealth, 1, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 3, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 2, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 6, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 7, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 12, PriorityTier.A);
        Add(UserGoal.MaintainHealth, 8, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 10, PriorityTier.C);
        Add(UserGoal.MaintainHealth, 11, PriorityTier.C);
        Add(UserGoal.MaintainHealth, 5, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 9, PriorityTier.B);
        Add(UserGoal.MaintainHealth, 4, PriorityTier.C);
        Add(UserGoal.MaintainHealth, 13, PriorityTier.C);

        GoalTierWeights.AddRange(rows);
        await SaveChangesAsync();
    }

    private async Task SeedVolumeBudgetRowsAsync()
    {
        if (await VolumeBudgetRows.AnyAsync())
            return;

        var rows = new List<VolumeBudgetRow>();
        int id = 1;

        void Add(int muscleId, double d2, double d3, double d4, double d5, double d6)
        {
            rows.Add(new VolumeBudgetRow { Id = id++, DaysPerWeek = 2, MuscleGroupId = muscleId, FractionalSets = d2 });
            rows.Add(new VolumeBudgetRow { Id = id++, DaysPerWeek = 3, MuscleGroupId = muscleId, FractionalSets = d3 });
            rows.Add(new VolumeBudgetRow { Id = id++, DaysPerWeek = 4, MuscleGroupId = muscleId, FractionalSets = d4 });
            rows.Add(new VolumeBudgetRow { Id = id++, DaysPerWeek = 5, MuscleGroupId = muscleId, FractionalSets = d5 });
            rows.Add(new VolumeBudgetRow { Id = id++, DaysPerWeek = 6, MuscleGroupId = muscleId, FractionalSets = d6 });
        }

        //              muscle  2d   3d   4d   5d   6d
        Add(/* Glutes */    1, 11, 14, 18, 21, 24);
        Add(/* Hamstrings */2, 7, 9, 12, 14, 15);
        Add(/* Abductors */ 3, 5, 6, 8, 10, 10);
        Add(/* Lats */      6, 4, 5, 7, 8, 8);
        Add(/* UpperBack */ 7, 3, 4, 5, 6, 7);
        Add(/* Delts */     8, 4, 6, 8, 10, 10);
        Add(/* Core */     12, 4, 6, 8, 9, 10);
        Add(/* Biceps */   10, 1.5, 2.5, 3.5, 4, 4.5);
        Add(/* Triceps */  11, 1.5, 2.5, 3.5, 4, 4.5);
        Add(/* Quads */     5, 5, 6, 8, 9, 10);
        Add(/* Adductors */ 4, 2, 3, 4, 4, 4);
        Add(/* Chest */     9, 2, 3, 4, 5, 6);
        Add(/* Calves */   13, 1, 2, 3, 4, 4);

        VolumeBudgetRows.AddRange(rows);
        await SaveChangesAsync();
    }

    private async Task SeedWorkoutExerciseDefinitionsAsync()
    {
        if (await WorkoutExerciseDefinitions.AnyAsync())
            return;

        var exercises = BuildWorkoutExerciseDefinitions();
        WorkoutExerciseDefinitions.AddRange(exercises);
        await SaveChangesAsync();
    }

    private static List<WorkoutExerciseDefinition> BuildWorkoutExerciseDefinitions()
    {
        var bb = EquipmentSet.Barbell;
        var db = EquipmentSet.Dumbbell;
        var mc = EquipmentSet.Machines;
        var cb = EquipmentSet.Cables;
        var bd = EquipmentSet.Bands;
        var htb = EquipmentSet.HipThrustBench;
        var be45 = EquipmentSet.BackExtension45;
        var pu = EquipmentSet.PullUpBar;
        var cw = EquipmentSet.ClimbingWall;

        return
        [
            // Glute / hip extension
            Ex(101, "Barbell Hip Thrust", bb | htb,
                ll: 1, ftg: 3, skl: 2, setup: 180, spr: 4, hi: true),
            Ex(102, "Machine Hip Thrust", mc,
                ll: 1, ftg: 3, skl: 1, setup: 60, spr: 4, hi: true),
            Ex(103, "B-Stance Hip Thrust", bb | htb,
                ll: 1, ftg: 2, skl: 2, setup: 150, spr: 4, uni: true),
            Ex(104, "Single-Leg Glute Bridge", EquipmentSet.None,
                ll: 1, ftg: 1, skl: 1, setup: 30, spr: 4, uni: true, bw: true),
            Ex(105, "Cable Pull-Through", cb,
                ll: 2, ftg: 2, skl: 1, setup: 60, spr: 4),
            Ex(106, "45 Degree Back Extension", be45,
                ll: 2, ftg: 2, skl: 2, setup: 45, spr: 4),
            Ex(107, "Kneeling Cable Kickback", cb,
                ll: 1, ftg: 1, skl: 1, setup: 60, spr: 4, uni: true),

            // Hinge / hamstring
            Ex(111, "Romanian Deadlift", bb,
                ll: 2, ftg: 4, skl: 2, setup: 180, spr: 5, hi: true, valsalva: 2),
            Ex(112, "Deficit RDL", bb,
                ll: 2, ftg: 4, skl: 3, setup: 180, spr: 5, hi: true, valsalva: 2),
            Ex(113, "Dumbbell RDL", db,
                ll: 2, ftg: 3, skl: 2, setup: 60, spr: 4),
            Ex(114, "Single-Leg RDL", db,
                ll: 2, ftg: 2, skl: 3, setup: 60, spr: 4, uni: true),
            Ex(115, "Seated Leg Curl", mc,
                ll: 2, ftg: 2, skl: 1, setup: 45, spr: 4),
            Ex(116, "Lying Leg Curl", mc,
                ll: 1, ftg: 2, skl: 1, setup: 45, spr: 4, supine: 2),
            Ex(117, "Nordic Curl", EquipmentSet.None,
                ll: 2, ftg: 4, skl: 3, setup: 60, spr: 5, bw: true),
            Ex(118, "Good Morning", bb,
                ll: 2, ftg: 4, skl: 3, setup: 180, spr: 5, valsalva: 2),

            // Squat / lunge
            Ex(121, "Back Squat", bb,
                ll: 2, ftg: 5, skl: 3, setup: 180, spr: 5, hi: true, valsalva: 2),
            Ex(122, "Hack Squat", mc,
                ll: 2, ftg: 4, skl: 1, setup: 60, spr: 5, hi: true),
            Ex(123, "Leg Press", mc,
                ll: 1, ftg: 3, skl: 1, setup: 60, spr: 4, hi: true),
            Ex(124, "Goblet Squat", db,
                ll: 2, ftg: 2, skl: 1, setup: 45, spr: 4),
            Ex(125, "Bulgarian Split Squat", db,
                ll: 2, ftg: 3, skl: 2, setup: 60, spr: 4, uni: true),
            Ex(126, "Walking Lunge", db,
                ll: 1, ftg: 3, skl: 2, setup: 60, spr: 4, uni: true),
            Ex(127, "Step-Up", db,
                ll: 1, ftg: 2, skl: 2, setup: 60, spr: 4, uni: true),
            Ex(128, "Reverse Lunge", db,
                ll: 1, ftg: 2, skl: 1, setup: 60, spr: 4, uni: true),

            // Abductors and adductors
            Ex(131, "Seated Hip Abduction Lean", mc,
                ll: 1, ftg: 1, skl: 1, setup: 45, spr: 4),
            Ex(132, "Seated Hip Abduction Upright", mc,
                ll: 1, ftg: 1, skl: 1, setup: 45, spr: 4),
            Ex(133, "Standing Cable Abduction", cb,
                ll: 1, ftg: 1, skl: 1, setup: 60, spr: 4, uni: true),
            Ex(134, "Banded Lateral Walk", bd,
                ll: 0, ftg: 1, skl: 1, setup: 20, spr: 3),
            Ex(135, "Side-Lying Abduction", EquipmentSet.None,
                ll: 1, ftg: 1, skl: 1, setup: 20, spr: 4, uni: true, bw: true),
            Ex(136, "Copenhagen Plank", EquipmentSet.None,
                ll: 1, ftg: 2, skl: 3, setup: 20, spr: 4, uni: true, bw: true),
            Ex(137, "Seated Adduction Machine", mc,
                ll: 1, ftg: 1, skl: 1, setup: 45, spr: 4),

            // Back
            Ex(141, "Pull-Up", pu,
                ll: 2, ftg: 3, skl: 2, setup: 30, spr: 4, hi: true, bw: true),
            Ex(142, "Lat Pulldown", mc | cb,
                ll: 2, ftg: 2, skl: 1, setup: 45, spr: 4, hi: true),
            Ex(143, "Chest-Supported Row", mc | db,
                ll: 1, ftg: 2, skl: 1, setup: 60, spr: 4, hi: true),
            Ex(144, "Seated Cable Row", cb,
                ll: 1, ftg: 2, skl: 1, setup: 45, spr: 4, hi: true),
            Ex(145, "Single-Arm DB Row", db,
                ll: 1, ftg: 2, skl: 1, setup: 60, spr: 4, uni: true),
            Ex(146, "Straight-Arm Pulldown", cb,
                ll: 2, ftg: 1, skl: 1, setup: 45, spr: 4),
            Ex(147, "Face Pull", cb,
                ll: 1, ftg: 1, skl: 1, setup: 45, spr: 4),

            // Push / delts / arms
            Ex(151, "DB Shoulder Press", db,
                ll: 1, ftg: 3, skl: 2, setup: 60, spr: 4, hi: true),
            Ex(152, "Machine Shoulder Press", mc,
                ll: 1, ftg: 2, skl: 1, setup: 45, spr: 4, hi: true),
            Ex(153, "Incline DB Press", db,
                ll: 2, ftg: 3, skl: 2, setup: 60, spr: 4, hi: true),
            Ex(154, "Machine Chest Press", mc,
                ll: 1, ftg: 2, skl: 1, setup: 45, spr: 4, hi: true),
            Ex(155, "Push-Up", EquipmentSet.None,
                ll: 1, ftg: 2, skl: 1, setup: 15, spr: 4, bw: true),
            Ex(156, "Cable Lateral Raise", cb,
                ll: 2, ftg: 1, skl: 1, setup: 45, spr: 4, uni: true),
            Ex(157, "DB Lateral Raise", db,
                ll: 1, ftg: 1, skl: 1, setup: 30, spr: 4),
            Ex(158, "Reverse Pec Deck", mc,
                ll: 1, ftg: 1, skl: 1, setup: 45, spr: 4),
            Ex(161, "Incline DB Curl", db,
                ll: 2, ftg: 1, skl: 1, setup: 45, spr: 4),
            Ex(162, "Cable Curl", cb,
                ll: 1, ftg: 1, skl: 1, setup: 45, spr: 4),
            Ex(163, "Overhead Cable Extension", cb,
                ll: 2, ftg: 1, skl: 1, setup: 45, spr: 4),
            Ex(164, "Rope Pushdown", cb,
                ll: 1, ftg: 1, skl: 1, setup: 45, spr: 4),

            // Core
            Ex(171, "Dead Bug", EquipmentSet.None,
                ll: 0, ftg: 1, skl: 1, setup: 15, spr: 4, bw: true),
            Ex(172, "Ab Wheel Rollout", EquipmentSet.None,
                ll: 2, ftg: 2, skl: 3, setup: 20, spr: 5, bw: true),
            Ex(173, "Hollow Hold", EquipmentSet.None,
                ll: 0, ftg: 1, skl: 2, setup: 15, spr: 0, bw: true,
                supine: 1),
            Ex(174, "Cable Crunch", cb,
                ll: 1, ftg: 2, skl: 1, setup: 45, spr: 4),
            Ex(175, "Pallof Press", cb,
                ll: 0, ftg: 1, skl: 1, setup: 45, spr: 4, uni: true),
            Ex(176, "Bird Dog", EquipmentSet.None,
                ll: 0, ftg: 1, skl: 1, setup: 15, spr: 4, uni: true, bw: true),
            Ex(177, "Side Plank", EquipmentSet.None,
                ll: 0, ftg: 1, skl: 1, setup: 15, spr: 0, uni: true, bw: true),
            Ex(178, "Suitcase Carry", db,
                ll: 0, ftg: 2, skl: 1, setup: 45, spr: 0, uni: true),

            // Calves
            Ex(181, "Standing Calf Raise", mc,
                ll: 2, ftg: 1, skl: 1, setup: 45, spr: 3),
            Ex(182, "Seated Calf Raise", mc,
                ll: 2, ftg: 1, skl: 1, setup: 45, spr: 3),

            // Climbing
            Ex(191, "Rock Climbing", cw,
                ll: 1, ftg: 4, skl: 2, setup: 0, spr: 0),
        ];
    }

    private static WorkoutExerciseDefinition Ex(
        int id, string name, EquipmentSet required,
        byte ll, byte ftg, byte skl, int setup, byte spr,
        bool hi = false, bool uni = false, bool bw = false,
        byte supine = 0, byte valsalva = 0, int? subGroup = null)
    {
        return new WorkoutExerciseDefinition
        {
            Id = id,
            Name = name,
            Required = required,
            LongLengthLoaded = ll,
            FatigueCost = ftg,
            SkillDemand = skl,
            SetupSeconds = setup,
            SecondsPerRep = spr,
            EligibleForHiBlock = hi,
            UnilateralDoublesTime = uni,
            IsBodyweight = bw,
            SupineOrProne = supine,
            ValsalvaDemand = valsalva,
            SubstituteGroupId = subGroup
        };
    }

    private async Task SeedExerciseMuscleContributionsAsync()
    {
        if (await ExerciseMuscleContributions.AnyAsync())
            return;

        var rows = new List<ExerciseMuscleContribution>();
        int id = 1;

        void C(int exId, int muscleId, double fraction)
            => rows.Add(new ExerciseMuscleContribution
            { Id = id++, ExerciseId = exId, MuscleGroupId = muscleId, Fraction = fraction });

        // Glute / hip extension
        C(101, 1, 1.0); C(101, 2, 0.5);  // Barbell Hip Thrust
        C(102, 1, 1.0); C(102, 2, 0.5);  // Machine Hip Thrust
        C(103, 1, 1.0); C(103, 2, 0.5);  // B-Stance Hip Thrust
        C(104, 1, 1.0); C(104, 2, 0.5);  // SL Glute Bridge
        C(105, 1, 1.0); C(105, 2, 0.5);  // Cable Pull-Through
        C(106, 1, 1.0); C(106, 2, 1.0); C(106, 7, 0.5);  // 45 Degree Back Extension
        C(107, 1, 1.0);  // Kneeling Cable Kickback

        // Hinge / hamstring
        C(111, 2, 1.0); C(111, 1, 0.5); C(111, 7, 0.5); C(111, 12, 0.5);  // Romanian Deadlift
        C(112, 2, 1.0); C(112, 1, 0.5); C(112, 7, 0.5); C(112, 12, 0.5);  // Deficit RDL
        C(113, 2, 1.0); C(113, 1, 0.5); C(113, 7, 0.5);  // Dumbbell RDL
        C(114, 2, 1.0); C(114, 1, 0.5); C(114, 3, 0.5); C(114, 12, 0.5);  // Single-Leg RDL
        C(115, 2, 1.0);  // Seated Leg Curl
        C(116, 2, 1.0);  // Lying Leg Curl
        C(117, 2, 1.0);  // Nordic Curl
        C(118, 2, 1.0); C(118, 1, 0.5); C(118, 7, 0.5);  // Good Morning

        // Squat / lunge
        C(121, 5, 1.0); C(121, 1, 1.0); C(121, 4, 0.5); C(121, 12, 0.5);  // Back Squat
        C(122, 5, 1.0); C(122, 1, 0.5);  // Hack Squat
        C(123, 1, 1.0); C(123, 5, 1.0); C(123, 4, 0.5);  // Leg Press
        C(124, 5, 1.0); C(124, 1, 0.5); C(124, 12, 0.5);  // Goblet Squat
        C(125, 1, 1.0); C(125, 5, 1.0); C(125, 2, 0.5); C(125, 3, 0.5);  // Bulgarian Split Squat
        C(126, 1, 1.0); C(126, 5, 1.0); C(126, 3, 0.5);  // Walking Lunge
        C(127, 1, 1.0); C(127, 5, 0.5); C(127, 3, 0.5);  // Step-Up
        C(128, 1, 1.0); C(128, 5, 0.5); C(128, 3, 0.5);  // Reverse Lunge

        // Abductors / adductors
        C(131, 3, 1.0); C(131, 1, 0.5);  // Seated Abduction (lean)
        C(132, 3, 1.0); C(132, 1, 0.5);  // Seated Abduction (upright)
        C(133, 3, 1.0); C(133, 1, 0.5);  // Standing Cable Abduction
        C(134, 3, 1.0); C(134, 1, 0.5);  // Banded Lateral Walk
        C(135, 3, 1.0);  // Side-Lying Abduction
        C(136, 4, 1.0); C(136, 12, 0.5);  // Copenhagen Plank
        C(137, 4, 1.0);  // Adduction Machine

        // Back
        C(141, 6, 1.0); C(141, 10, 0.5); C(141, 12, 0.5);  // Pull-Up
        C(142, 6, 1.0); C(142, 10, 0.5);  // Lat Pulldown
        C(143, 7, 1.0); C(143, 6, 0.5); C(143, 10, 0.5);  // Chest-Supported Row
        C(144, 7, 1.0); C(144, 6, 0.5); C(144, 10, 0.5);  // Seated Cable Row
        C(145, 7, 1.0); C(145, 6, 0.5); C(145, 10, 0.5);  // Single-Arm DB Row
        C(146, 6, 1.0);  // Straight-Arm Pulldown
        C(147, 8, 1.0); C(147, 7, 0.5);  // Face Pull

        // Push / delts / arms
        C(151, 8, 1.0); C(151, 11, 0.5);  // DB Shoulder Press
        C(152, 8, 1.0); C(152, 11, 0.5);  // Machine Shoulder Press
        C(153, 9, 1.0); C(153, 8, 0.5); C(153, 11, 0.5);  // Incline DB Press
        C(154, 9, 1.0); C(154, 8, 0.5); C(154, 11, 0.5);  // Machine Chest Press
        C(155, 9, 1.0); C(155, 11, 0.5); C(155, 12, 0.5);  // Push-Up
        C(156, 8, 1.0);  // Cable Lateral Raise
        C(157, 8, 1.0);  // DB Lateral Raise
        C(158, 8, 1.0); C(158, 7, 0.5);  // Reverse Pec Deck
        C(161, 10, 1.0);  // Incline DB Curl
        C(162, 10, 1.0);  // Cable Curl
        C(163, 11, 1.0);  // Overhead Cable Extension
        C(164, 11, 1.0);  // Rope Pushdown

        // Core
        C(171, 12, 1.0);  // Dead Bug
        C(172, 12, 1.0); C(172, 6, 0.5);  // Ab Wheel Rollout
        C(173, 12, 1.0);  // Hollow Hold
        C(174, 12, 1.0);  // Cable Crunch
        C(175, 12, 1.0);  // Pallof Press
        C(176, 12, 1.0);  // Bird Dog
        C(177, 12, 1.0);  // Side Plank
        C(178, 12, 1.0); C(178, 7, 0.5);  // Suitcase Carry

        // Calves
        C(181, 13, 1.0);  // Standing Calf Raise
        C(182, 13, 1.0);  // Seated Calf Raise

        // Climbing
        C(191, 6, 1.0); C(191, 7, 0.5); C(191, 10, 0.5); C(191, 12, 0.5); C(191, 5, 0.5);

        ExerciseMuscleContributions.AddRange(rows);
        await SaveChangesAsync();
    }

    private async Task SeedSessionArchetypesAsync()
    {
        if (await SessionArchetypes.AnyAsync())
            return;

        SessionArchetypes.AddRange(
            new SessionArchetype { Id = 1, Code = "P", DisplayName = "Posterior / Thrust", IsLowerDominant = true, ContainsHeavyHinge = true },
            new SessionArchetype { Id = 2, Code = "S", DisplayName = "Squat / Abduction", IsLowerDominant = true, ContainsHeavyHinge = false },
            new SessionArchetype { Id = 3, Code = "U", DisplayName = "Upper (pull emphasis)", IsLowerDominant = false, ContainsHeavyHinge = false },
            new SessionArchetype { Id = 4, Code = "U2", DisplayName = "Upper (push emphasis)", IsLowerDominant = false, ContainsHeavyHinge = false },
            new SessionArchetype { Id = 5, Code = "F", DisplayName = "Full-body glute-biased", IsLowerDominant = true, ContainsHeavyHinge = true },
            new SessionArchetype { Id = 6, Code = "G", DisplayName = "Glute / Abductor short", IsLowerDominant = true, ContainsHeavyHinge = false },
            new SessionArchetype { Id = 7, Code = "C", DisplayName = "Climbing", IsLowerDominant = false, ContainsHeavyHinge = false });

        await SaveChangesAsync();
    }

    private async Task SeedSlotTemplatesAsync()
    {
        if (await SlotTemplates.AnyAsync())
            return;

        var slots = new List<SlotTemplate>();
        int id = 1;

        SlotTemplate Slot(int archetypeId, byte order, BlockType block,
            int primaryMuscleId,
            byte setsMin, byte setsMax, byte repsMin, byte repsMax,
            byte rir, bool droppable = false, byte ssGroup = 0)
        {
            var s = new SlotTemplate
            {
                Id = id++,
                ArchetypeId = archetypeId,
                Order = order,
                Block = block,
                PrimaryMuscleId = primaryMuscleId,
                SetsMin = setsMin,
                SetsMax = setsMax,
                RepsMin = repsMin,
                RepsMax = repsMax,
                TargetRir = rir,
                Droppable = droppable,
                SupersetGroup = ssGroup
            };
            return s;
        }

        // Archetype P - Posterior / Thrust
        slots.Add(Slot(1, 1, BlockType.HighIntensity, 1, 2, 3, 5, 7, 1));
        slots.Add(Slot(1, 2, BlockType.Hypertrophy, 2, 3, 4, 8, 10, 2));
        slots.Add(Slot(1, 3, BlockType.Hypertrophy, 1, 3, 4, 10, 12, 2));
        slots.Add(Slot(1, 4, BlockType.Accessory, 2, 2, 3, 10, 15, 1, true, 1));
        slots.Add(Slot(1, 5, BlockType.Accessory, 3, 2, 3, 15, 20, 1, false, 1));
        slots.Add(Slot(1, 6, BlockType.Core, 12, 2, 3, 8, 15, 2, true, 2));

        // Archetype S - Squat / Abduction
        slots.Add(Slot(2, 1, BlockType.HighIntensity, 5, 2, 3, 5, 7, 1));
        slots.Add(Slot(2, 2, BlockType.Hypertrophy, 1, 3, 4, 8, 12, 2));
        slots.Add(Slot(2, 3, BlockType.Hypertrophy, 1, 3, 4, 10, 15, 2));
        slots.Add(Slot(2, 4, BlockType.Accessory, 3, 3, 3, 15, 20, 1, false, 1));
        slots.Add(Slot(2, 5, BlockType.Accessory, 4, 2, 2, 12, 20, 2, true, 1));
        slots.Add(Slot(2, 6, BlockType.Core, 12, 2, 3, 8, 12, 2, true, 2));

        // Archetype U - Upper (pull emphasis)
        slots.Add(Slot(3, 1, BlockType.HighIntensity, 6, 2, 3, 5, 7, 1));
        slots.Add(Slot(3, 2, BlockType.Hypertrophy, 7, 3, 4, 8, 12, 2));
        slots.Add(Slot(3, 3, BlockType.Hypertrophy, 8, 3, 3, 8, 12, 2, true));
        slots.Add(Slot(3, 4, BlockType.Accessory, 8, 3, 3, 12, 20, 1, false, 1));
        slots.Add(Slot(3, 5, BlockType.Accessory, 10, 2, 3, 10, 15, 1, true, 1));
        slots.Add(Slot(3, 6, BlockType.Core, 12, 2, 2, 8, 15, 2, true, 2));

        // Archetype U2 - Upper (push emphasis)
        slots.Add(Slot(4, 1, BlockType.HighIntensity, 8, 2, 3, 5, 7, 1));
        slots.Add(Slot(4, 2, BlockType.Hypertrophy, 7, 3, 4, 8, 12, 2));
        slots.Add(Slot(4, 3, BlockType.Hypertrophy, 6, 3, 3, 10, 12, 2));
        slots.Add(Slot(4, 4, BlockType.Accessory, 8, 3, 3, 12, 20, 1, false, 1));
        slots.Add(Slot(4, 5, BlockType.Accessory, 11, 2, 3, 10, 15, 1, true, 1));
        slots.Add(Slot(4, 6, BlockType.Core, 12, 2, 2, 8, 15, 2, true, 2));

        // Archetype F - Full-body glute-biased
        slots.Add(Slot(5, 1, BlockType.HighIntensity, 1, 2, 3, 5, 7, 1));
        slots.Add(Slot(5, 2, BlockType.Hypertrophy, 2, 3, 4, 8, 10, 2));
        slots.Add(Slot(5, 3, BlockType.Hypertrophy, 6, 3, 4, 8, 12, 2));
        slots.Add(Slot(5, 4, BlockType.Hypertrophy, 1, 3, 3, 10, 12, 2));
        slots.Add(Slot(5, 5, BlockType.Accessory, 3, 2, 3, 15, 20, 1, false, 1));
        slots.Add(Slot(5, 6, BlockType.Accessory, 8, 2, 3, 10, 15, 1, true, 1));
        slots.Add(Slot(5, 7, BlockType.Core, 12, 2, 3, 8, 15, 2, true, 2));
        slots.Add(Slot(5, 8, BlockType.Accessory, 9, 2, 2, 10, 15, 2, true, 3));
        slots.Add(Slot(5, 9, BlockType.Accessory, 13, 2, 2, 12, 20, 2, true, 3));

        // Archetype G - Glute / Abductor short
        slots.Add(Slot(6, 1, BlockType.Hypertrophy, 1, 3, 4, 10, 12, 2));
        slots.Add(Slot(6, 2, BlockType.Hypertrophy, 1, 3, 3, 10, 15, 2, false, 1));
        slots.Add(Slot(6, 3, BlockType.Accessory, 3, 3, 3, 15, 20, 1, false, 1));
        slots.Add(Slot(6, 4, BlockType.Accessory, 3, 2, 2, 20, 25, 1, true, 2));

        // Archetype C - Climbing
        slots.Add(Slot(7, 1, BlockType.Hypertrophy, 6, 1, 1, 0, 0, 0));

        SlotTemplates.AddRange(slots);
        await SaveChangesAsync();
    }

    private async Task SeedWeekTemplatesAsync()
    {
        if (await WeekTemplates.AnyAsync())
            return;

        var templates = new List<WeekTemplate>();
        int id = 1;

        WeekTemplate Wt(int days, params int[] sequence)
        {
            var t = new WeekTemplate { Id = id++, DaysPerWeek = days };
            t.SetArchetypeSequence(sequence);
            return t;
        }

        templates.Add(Wt(2, 5, 5));       // F, F
        templates.Add(Wt(3, 1, 3, 2));    // P, U, S
        templates.Add(Wt(4, 1, 3, 2, 4)); // P, U, S, U2(+G merged)
        templates.Add(Wt(5, 1, 3, 6, 2, 4)); // P, U, G, S, U2
        templates.Add(Wt(6, 1, 3, 6, 2, 4, 0)); // P, U, G, S, U2, Recovery (0 = no strength archetype)

        WeekTemplates.AddRange(templates);
        await SaveChangesAsync();
    }
}
