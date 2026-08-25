using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

public class NutritionPlanner : INutritionPlanner
{
    private const float DefaultCalories = 2000f;
    private const float DefaultProtein = 120f;
    private const float DefaultCarbs = 200f;
    private const float DefaultFats = 65f;
    private const float MinimumCalories = 1200f;
    private const float MaximumCalories = 4000f;

    private readonly ICalorieCalculator _calorieCalculator;

    public NutritionPlanner(ICalorieCalculator calorieCalculator)
    {
        _calorieCalculator = calorieCalculator;
    }

    public NutritionPlan CreateDailyPlan(UserProfile profile, CyclePhase phase, DateTime date)
    {
        if (!HasUsableMetrics(profile))
            return CreateFallbackPlan(phase);

        var age = CalculateAge(profile.DateOfBirth, date);
        var bmr = _calorieCalculator.CalculateBmr(profile.Weight, profile.Height, age);
        var activityMultiplier = CalculateActivityMultiplier(
            profile.WorkoutDaysPerWeek,
            profile.TrainingExperienceLevel);
        var tdee = bmr * activityMultiplier;
        var goalAdjustment = CalculateGoalAdjustment(
            tdee,
            profile.Goal,
            profile.WeightGoalPace);
        var phaseAdjustment = CalculatePhaseAdjustment(phase, profile.Goal);
        var calories = RoundToNearest(
            _calorieCalculator.Clamp(
                tdee + goalAdjustment + phaseAdjustment,
                MinimumCalories,
                MaximumCalories),
            10f);
        var macros = CalculateMacros(calories, profile.Weight, profile.Goal, phase);
        var fiber = CalculateFiber(calories, phase);
        var water = CalculateWaterLiters(profile.Weight, profile.WorkoutDaysPerWeek, phase);
        var calculatedGoals = ProfileNutritionGoals.FromCalculated(
            calories,
            macros.Protein,
            macros.Carbs,
            macros.Fats,
            fiber,
            water);
        var customGoals = ProfileNutritionGoals.FromJson(profile.NutritionGoalsJson);
        var goals = customGoals.HasAnyValue
            ? customGoals.WithFallbacks(calculatedGoals)
            : calculatedGoals;
        calories = goals.Calories ?? calories;
        macros = (
            goals.Protein ?? macros.Protein,
            goals.Carbs ?? macros.Carbs,
            goals.Fats ?? macros.Fats);
        fiber = goals.Fiber ?? fiber;
        water = goals.WaterLiters ?? water;

        return new NutritionPlan(
            calories,
            macros.Protein,
            macros.Carbs,
            macros.Fats,
            fiber,
            water,
            RoundToNearest(bmr, 1f),
            RoundToNearest(tdee, 1f),
            activityMultiplier,
            RoundToNearest(goalAdjustment, 1f),
            phaseAdjustment,
            phase,
            GetPhaseFocus(phase),
            BuildNotes(profile),
            goals,
            BuildMealTargets(calories, macros.Protein, macros.Carbs, macros.Fats));
    }

    public NutritionPlan CreateFallbackPlan(CyclePhase phase)
    {
        return new NutritionPlan(
            DefaultCalories,
            DefaultProtein,
            DefaultCarbs,
            DefaultFats,
            28f,
            2.3f,
            0f,
            0f,
            0f,
            0f,
            0f,
            phase,
            GetPhaseFocus(phase),
            ["Complete profile setup to personalize targets."],
            ProfileNutritionGoals.FromCalculated(DefaultCalories, DefaultProtein, DefaultCarbs, DefaultFats, 28f, 2.3f),
            BuildMealTargets(DefaultCalories, DefaultProtein, DefaultCarbs, DefaultFats));
    }

    private static bool HasUsableMetrics(UserProfile profile) =>
        profile.Height >= 100f &&
        profile.Weight >= 30f &&
        profile.DateOfBirth.Year > 1900;

    private static int CalculateAge(DateTime dateOfBirth, DateTime date)
    {
        var age = date.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > date.Date.AddYears(-age))
            age--;

        return Math.Clamp(age, 12, 100);
    }

    private static float CalculateActivityMultiplier(
        int workoutDaysPerWeek,
        TrainingExperienceLevel experienceLevel)
    {
        var baseMultiplier = workoutDaysPerWeek switch
        {
            <= 0 => 1.2f,
            <= 2 => 1.375f,
            <= 4 => 1.55f,
            <= 6 => 1.725f,
            _ => 1.8f
        };

        var experienceAdjustment = experienceLevel switch
        {
            TrainingExperienceLevel.Beginner => -0.025f,
            TrainingExperienceLevel.Advanced => 0.025f,
            _ => 0f
        };

        return Math.Clamp(baseMultiplier + experienceAdjustment, 1.2f, 1.85f);
    }

    private static float CalculateGoalAdjustment(
        float tdee,
        UserGoal goal,
        WeightGoalPace pace)
    {
        var isAggressive = pace is WeightGoalPace.Aggressive;

        return goal switch
        {
            UserGoal.FatLoss => -MathF.Min(tdee * (isAggressive ? 0.20f : 0.12f), isAggressive ? 500f : 300f),
            UserGoal.Strength => MathF.Min(tdee * (isAggressive ? 0.12f : 0.08f), isAggressive ? 400f : 250f),
            UserGoal.MuscleTone => MathF.Min(tdee * 0.04f, 150f),
            _ => 0f
        };
    }

    private static float CalculatePhaseAdjustment(CyclePhase phase, UserGoal goal)
    {
        var adjustment = phase switch
        {
            CyclePhase.Menstrual => -50f,
            CyclePhase.Ovulatory => 50f,
            CyclePhase.Luteal => 150f,
            _ => 0f
        };

        return goal is UserGoal.FatLoss && phase is CyclePhase.Menstrual
            ? 0f
            : adjustment;
    }

    private static (float Protein, float Carbs, float Fats) CalculateMacros(
        float calories,
        float weightKg,
        UserGoal goal,
        CyclePhase phase)
    {
        var proteinPerKg = goal switch
        {
            UserGoal.FatLoss => 2.0f,
            UserGoal.Strength => 1.9f,
            UserGoal.MuscleTone => 1.8f,
            _ => 1.6f
        };

        var protein = RoundToNearest(weightKg * proteinPerKg, 1f);
        var fatPercent = phase switch
        {
            CyclePhase.Menstrual => 0.30f,
            CyclePhase.Luteal => 0.30f,
            _ => 0.25f
        };

        var fats = MathF.Max(weightKg * 0.7f, calories * fatPercent / 9f);
        var minimumCarbCalories = calories * 0.25f;
        var caloriesAfterProtein = calories - protein * 4f;

        if (caloriesAfterProtein - fats * 9f < minimumCarbCalories)
            fats = MathF.Max(weightKg * 0.6f, (caloriesAfterProtein - minimumCarbCalories) / 9f);

        fats = RoundToNearest(MathF.Max(fats, 0f), 1f);
        var carbs = RoundToNearest(MathF.Max((calories - protein * 4f - fats * 9f) / 4f, 0f), 1f);

        return (protein, carbs, fats);
    }

    private static float CalculateFiber(float calories, CyclePhase phase)
    {
        var baseline = MathF.Max(25f, calories / 1000f * 14f);
        var phaseBonus = phase is CyclePhase.Luteal ? 3f : 0f;
        return RoundToNearest(baseline + phaseBonus, 1f);
    }

    private static float CalculateWaterLiters(
        float weightKg,
        int workoutDaysPerWeek,
        CyclePhase phase)
    {
        var trainingBonus = Math.Clamp(workoutDaysPerWeek, 0, 7) * 0.1f;
        var phaseBonus = phase is CyclePhase.Luteal or CyclePhase.Ovulatory ? 0.2f : 0f;
        return RoundToNearest(Math.Clamp(weightKg * 0.035f + trainingBonus + phaseBonus, 1.8f, 4.0f), 0.1f);
    }

    private static string GetPhaseFocus(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => "Prioritize iron, protein, warm meals, and steady hydration.",
        CyclePhase.Follicular => "Use the rising-energy window for balanced protein and training carbs.",
        CyclePhase.Ovulatory => "Support peak output with protein, carbs, and extra fluids.",
        CyclePhase.Luteal => "Plan filling carbs, fiber, magnesium-rich foods, and steady snacks.",
        _ => "Keep meals balanced and consistent."
    };

    private static IReadOnlyCollection<string> BuildNotes(UserProfile profile)
    {
        var notes = new List<string>();

        if (profile.Goal is UserGoal.FatLoss)
            notes.Add("Keep the deficit moderate so protein and recovery stay protected.");
        if (profile.Goal is UserGoal.Strength)
            notes.Add("Use a small surplus to support performance and strength progress.");
        if (profile.TrainingExperienceLevel is TrainingExperienceLevel.Beginner)
            notes.Add("Start with consistent meals before fine-tuning small macro changes.");
        if (profile.DietaryTags.Contains(DietaryTag.Vegan.ToString(), StringComparison.OrdinalIgnoreCase))
            notes.Add("Plan complete plant proteins and pay attention to iron and B12 sources.");
        else if (profile.DietaryTags.Contains(DietaryTag.Vegetarian.ToString(), StringComparison.OrdinalIgnoreCase))
            notes.Add("Include reliable protein sources across meals.");
        if (profile.DietaryTags.Contains(DietaryTag.GlutenFree.ToString(), StringComparison.OrdinalIgnoreCase))
            notes.Add("Use gluten-free carb staples that still bring fiber.");
        if (profile.DietaryTags.Contains(DietaryTag.LactoseFree.ToString(), StringComparison.OrdinalIgnoreCase))
            notes.Add("Use lactose-free protein and calcium sources.");

        return notes;
    }

    private static IReadOnlyCollection<MealNutritionTarget> BuildMealTargets(
        float calories,
        float protein,
        float carbs,
        float fats) =>
    [
        BuildMealTarget(MealType.Breakfast, 0.25f, calories, protein, carbs, fats),
        BuildMealTarget(MealType.Lunch, 0.30f, calories, protein, carbs, fats),
        BuildMealTarget(MealType.Dinner, 0.30f, calories, protein, carbs, fats),
        BuildMealTarget(MealType.Snack, 0.15f, calories, protein, carbs, fats)
    ];

    private static MealNutritionTarget BuildMealTarget(
        MealType mealType,
        float share,
        float calories,
        float protein,
        float carbs,
        float fats) =>
        new(
            mealType,
            RoundToNearest(calories * share, 10f),
            RoundToNearest(protein * share, 1f),
            RoundToNearest(carbs * share, 1f),
            RoundToNearest(fats * share, 1f));

    private static float RoundToNearest(float value, float nearest) =>
        MathF.Round(value / nearest) * nearest;
}
