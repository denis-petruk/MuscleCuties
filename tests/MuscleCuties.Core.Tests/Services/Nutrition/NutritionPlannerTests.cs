using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Tests.Services.Nutrition;

public class NutritionPlannerTests
{
    private readonly NutritionPlanner _planner = new(new CalorieCalculator());
    private readonly DateTime _date = new(2026, 8, 11);

    [Fact]
    public void CreateDailyPlan_StrengthProfile_CreatesSmallSurplusAndMealTargets()
    {
        var profile = CreateProfile(
            UserGoal.Strength,
            workoutDaysPerWeek: 5,
            experienceLevel: TrainingExperienceLevel.Advanced);

        var plan = _planner.CreateDailyPlan(profile, CyclePhase.Follicular, _date);

        Assert.True(plan.Calories > plan.Tdee);
        Assert.True(plan.Protein >= profile.Weight * 1.9f);
        Assert.Equal(4, plan.Meals.Count);
        Assert.Contains(plan.Meals, meal => meal.MealType == MealType.Breakfast);
        Assert.Contains(plan.Notes, note => note.Contains("small surplus", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(plan.Meals.Sum(meal => meal.Calories), plan.Calories - 30f, plan.Calories + 30f);
    }

    [Fact]
    public void CreateDailyPlan_LutealPhase_AddsPhaseCaloriesFiberAndFocus()
    {
        var profile = CreateProfile(UserGoal.MaintainHealth);

        var follicular = _planner.CreateDailyPlan(profile, CyclePhase.Follicular, _date);
        var luteal = _planner.CreateDailyPlan(profile, CyclePhase.Luteal, _date);

        Assert.Equal(150f, luteal.PhaseAdjustment);
        Assert.True(luteal.Calories > follicular.Calories);
        Assert.True(luteal.Fiber > follicular.Fiber);
        Assert.Contains("filling carbs", luteal.PhaseFocus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDailyPlan_FatLossMenstrualPhase_DoesNotStackExtraPhaseDeficit()
    {
        var profile = CreateProfile(UserGoal.FatLoss);

        var plan = _planner.CreateDailyPlan(profile, CyclePhase.Menstrual, _date);

        Assert.Equal(0f, plan.PhaseAdjustment);
        Assert.True(plan.Calories < plan.Tdee);
        Assert.True(plan.Protein >= profile.Weight * 2f);
    }

    [Fact]
    public void CreateDailyPlan_InvalidMetrics_ReturnsFallbackPlan()
    {
        var profile = CreateProfile(UserGoal.MaintainHealth);
        profile.Height = 0f;
        profile.Weight = 0f;

        var plan = _planner.CreateDailyPlan(profile, CyclePhase.Ovulatory, _date);

        Assert.Equal(2000f, plan.Calories);
        Assert.Equal(120f, plan.Protein);
        Assert.Equal(CyclePhase.Ovulatory, plan.Phase);
        Assert.Contains("Complete profile setup", plan.Notes.Single());
    }

    [Fact]
    public void CreateDailyPlan_VeganProfile_AddsDietaryNotes()
    {
        var profile = CreateProfile(UserGoal.MuscleTone);
        profile.DietaryTags = DietaryTag.Vegan.ToString();

        var plan = _planner.CreateDailyPlan(profile, CyclePhase.Follicular, _date);

        Assert.Contains(plan.Notes, note => note.Contains("plant proteins", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Notes, note => note.Contains("B12", StringComparison.OrdinalIgnoreCase));
    }

    private static UserProfile CreateProfile(
        UserGoal goal,
        int workoutDaysPerWeek = 4,
        TrainingExperienceLevel experienceLevel = TrainingExperienceLevel.Intermediate) =>
        new()
        {
            UserId = 1,
            Name = "Test",
            DateOfBirth = new DateTime(1996, 6, 15),
            Height = 168f,
            Weight = 65f,
            Goal = goal,
            WeightGoalPace = WeightGoalPace.Steady,
            TrainingExperienceLevel = experienceLevel,
            WorkoutDaysPerWeek = workoutDaysPerWeek,
            CycleLength = 28,
            UpdatedAt = DateTime.UtcNow
        };
}
