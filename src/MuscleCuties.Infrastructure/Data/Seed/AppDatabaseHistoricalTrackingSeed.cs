using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Data;

public partial class AppDatabase
{
    private async Task SeedHistoricalTrackingDataAsync()
    {
        var user = await Users.FirstOrDefaultAsync().ConfigureAwait(false);
        if (user is null)
            return;

        var userId = user.Id;

        await SeedHistoricalFoodLogsAsync(userId).ConfigureAwait(false);
        await SeedHistoricalWorkoutSessionsAsync(userId).ConfigureAwait(false);
    }

    private async Task SeedHistoricalFoodLogsAsync(int userId)
    {
        if (await DailyNutritionLogs.AnyAsync().ConfigureAwait(false))
            return;

        var today = DateTime.Today;
        var now = DateTime.UtcNow;

        var sampleDays = new[]
        {
            (DaysAgo: 0, Water: 2400, Notes: "Solid energy today, stayed hydrated throughout training."),
            (DaysAgo: 1, Water: 2600, Notes: "Felt strong during workout, good macro split."),
            (DaysAgo: 2, Water: 2200, Notes: "Higher carb day for recovery."),
            (DaysAgo: 3, Water: 2500, Notes: "Focused on lean proteins and hydration.")
        };

        foreach (var sample in sampleDays)
        {
            var date = today.AddDays(-sample.DaysAgo);
            var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var log = new DailyNutritionLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = dateStr,
                WaterIntakeMl = sample.Water,
                TargetCalories = 2100,
                TargetProtein = 135,
                TargetCarbs = 220,
                TargetFat = 65,
                Notes = sample.Notes,
                CreatedAt = now.AddDays(-sample.DaysAgo),
                UpdatedAt = now.AddDays(-sample.DaysAgo)
            };

            var entries = new List<FoodItemEntry>
            {
                CreateSeedFoodEntry(log.Id, dateStr, MealType.Breakfast, "Oatmeal with Blueberries & Whey", 150, 420,
                    32, 54, 8, date.AddHours(8).ToUniversalTime(), now.AddDays(-sample.DaysAgo)),
                CreateSeedFoodEntry(log.Id, dateStr, MealType.Lunch, "Grilled Chicken Breast with Quinoa", 320, 580, 52,
                    58, 14, date.AddHours(12).AddMinutes(30).ToUniversalTime(), now.AddDays(-sample.DaysAgo)),
                CreateSeedFoodEntry(log.Id, dateStr, MealType.Dinner, "Baked Atlantic Salmon & Sweet Potato", 350, 620,
                    46, 48, 22, date.AddHours(19).ToUniversalTime(), now.AddDays(-sample.DaysAgo)),
                CreateSeedFoodEntry(log.Id, dateStr, MealType.Snack, "Greek Yogurt 0% with Raw Honey & Almonds", 200,
                    290, 22, 28, 9, date.AddHours(16).ToUniversalTime(), now.AddDays(-sample.DaysAgo))
            };

            log.TotalCalories = entries.Sum(e => e.Calories);
            log.TotalProtein = entries.Sum(e => e.Protein);
            log.TotalCarbs = entries.Sum(e => e.Carbs);
            log.TotalFat = entries.Sum(e => e.Fat);

            foreach (var e in entries)
                log.Entries.Add(e);

            await DailyNutritionLogs.AddAsync(log).ConfigureAwait(false);
            await FoodItemEntries.AddRangeAsync(entries).ConfigureAwait(false);
        }

        await SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task SeedHistoricalWorkoutSessionsAsync(int userId)
    {
        if (await WorkoutSessionLogs.AnyAsync().ConfigureAwait(false))
            return;

        var today = DateTime.Today;
        var now = DateTime.UtcNow;

        var sessions =
            new List<(int DaysAgo, string Name, int DurationMin, int Rpe, string Notes,
                List<(string Exercise, int Sets, double Weight, int Reps, int RestSec, double Rpe)> Exercises)>
            {
                (
                    DaysAgo: 1,
                    Name: "Upper Body Hypertrophy",
                    DurationMin: 54,
                    Rpe: 8,
                    Notes: "Clean form on barbell bench. Increased top set weight by 2.5 kg.",
                    Exercises:
                    [
                        ("Barbell Bench Press", 4, 75.0, 8, 120, 8.5),
                        ("Incline Dumbbell Press", 3, 26.0, 10, 90, 8.0),
                        ("Neutral Grip Lat Pulldown", 4, 65.0, 10, 90, 7.5),
                        ("Cable Rope Overhead Triceps", 3, 22.5, 12, 60, 8.0)
                    ]
                ),
                (
                    DaysAgo: 3,
                    Name: "Lower Body Quad & Posterior Chain",
                    DurationMin: 62,
                    Rpe: 9,
                    Notes: "Great depth on back squats. Legs felt fully recovered.",
                    Exercises:
                    [
                        ("Barbell Back Squat", 4, 95.0, 6, 150, 8.5),
                        ("Romanian Deadlift", 4, 85.0, 8, 120, 8.0),
                        ("Bulgarian Split Squats", 3, 18.0, 10, 90, 8.5),
                        ("Standing Calf Raises", 3, 50.0, 15, 60, 7.5)
                    ]
                ),
                (
                    DaysAgo: 5,
                    Name: "Push Power & Delts",
                    DurationMin: 48,
                    Rpe: 8,
                    Notes: "Focused on explosive concentric phase and 2s eccentric control.",
                    Exercises:
                    [
                        ("Standing Overhead Barbell Press", 4, 45.0, 8, 120, 8.0),
                        ("Weighted Dips", 3, 10.0, 10, 90, 8.5),
                        ("Cable Lateral Raises", 4, 7.5, 15, 60, 8.0),
                        ("Cross-body Cable Fly", 3, 12.5, 12, 60, 7.5)
                    ]
                ),
                (
                    DaysAgo: 7,
                    Name: "Pull & Core Dynamics",
                    DurationMin: 50,
                    Rpe: 7,
                    Notes: "Smooth tempo, good mind-muscle connection on lats and upper back.",
                    Exercises:
                    [
                        ("Barbell Bent-Over Row", 4, 65.0, 8, 120, 7.5),
                        ("Pull-Ups (Bodyweight)", 3, 0.0, 8, 90, 8.0),
                        ("Chest-Supported Row", 3, 40.0, 12, 75, 7.5),
                        ("Hanging Knee Raises", 3, 0.0, 15, 60, 7.0)
                    ]
                )
            };

        foreach (var s in sessions)
        {
            var startedAt = today.AddDays(-s.DaysAgo).AddHours(17).AddMinutes(15).ToUniversalTime();
            var durationSec = s.DurationMin * 60;
            var completedAt = startedAt.AddSeconds(durationSec);

            var sessionLog = new WorkoutSessionLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkoutName = s.Name,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                TotalDurationSeconds = durationSec,
                Notes = s.Notes,
                RpeRating = s.Rpe,
                CreatedAt = now.AddDays(-s.DaysAgo),
                UpdatedAt = now.AddDays(-s.DaysAgo)
            };

            double totalVol = 0;
            var setLogs = new List<ExerciseSetLog>();

            foreach (var ex in s.Exercises)
            {
                // Add 1 warmup set
                var warmup = new ExerciseSetLog
                {
                    Id = Guid.NewGuid(),
                    WorkoutSessionLogId = sessionLog.Id,
                    ExerciseId = string.Concat(ex.Exercise.Where(char.IsLetterOrDigit)).ToUpperInvariant(),
                    ExerciseName = ex.Exercise,
                    SetNumber = 1,
                    WeightKg = Math.Round(ex.Weight * 0.5, 1),
                    RepsCompleted = 10,
                    Rpe = 5.0,
                    IsWarmup = true,
                    RestTimeSeconds = 60,
                    CreatedAt = now.AddDays(-s.DaysAgo),
                    UpdatedAt = now.AddDays(-s.DaysAgo)
                };
                setLogs.Add(warmup);

                for (var setIdx = 1; setIdx <= ex.Sets; setIdx++)
                {
                    var workingSet = new ExerciseSetLog
                    {
                        Id = Guid.NewGuid(),
                        WorkoutSessionLogId = sessionLog.Id,
                        ExerciseId = string.Concat(ex.Exercise.Where(char.IsLetterOrDigit)).ToUpperInvariant(),
                        ExerciseName = ex.Exercise,
                        SetNumber = setIdx + 1,
                        WeightKg = ex.Weight,
                        RepsCompleted = ex.Reps,
                        Rpe = ex.Rpe,
                        IsWarmup = false,
                        RestTimeSeconds = ex.RestSec,
                        CreatedAt = now.AddDays(-s.DaysAgo),
                        UpdatedAt = now.AddDays(-s.DaysAgo)
                    };
                    totalVol += workingSet.WeightKg * workingSet.RepsCompleted;
                    setLogs.Add(workingSet);
                }
            }

            sessionLog.TotalVolumeKg = Math.Round(totalVol, 1);
            foreach (var sl in setLogs)
                sessionLog.SetLogs.Add(sl);

            await WorkoutSessionLogs.AddAsync(sessionLog).ConfigureAwait(false);
            await ExerciseSetLogs.AddRangeAsync(setLogs).ConfigureAwait(false);
        }

        await SaveChangesAsync().ConfigureAwait(false);
    }

    private static FoodItemEntry CreateSeedFoodEntry(Guid dailyLogId, string dateStr, MealType mealType, string name,
        double servingSize, double calories, double protein, double carbs, double fat, DateTime loggedAtUtc,
        DateTime createdAtUtc)
    {
        return new FoodItemEntry
        {
            Id = Guid.NewGuid(),
            DailyLogId = dailyLogId,
            Date = dateStr,
            MealType = mealType,
            Name = name,
            ServingSize = servingSize,
            ServingUnit = "g",
            ServingsCount = 1,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fat = fat,
            LoggedAtTimestamp = loggedAtUtc,
            CreatedAt = createdAtUtc,
            UpdatedAt = createdAtUtc
        };
    }
}
