using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Infrastructure.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

/// <summary>Turns a profile into daily targets: calories from the calculator, then macros, fibre and a per-meal split that</summary>
/// respects the phase's focus.</summary>
public class NutritionPlanner : INutritionPlanner
{
    private const float DefaultCalories = 2000f;
    private const float DefaultProtein = 120f;
    private const float DefaultCarbs = 200f;
    private const float DefaultFats = 65f;
    private const float DefaultFiberGrams = 28f;
    private const float DefaultWaterLiters = 2.3f;
    private const float MinimumDailyCalories = 1200f;
    private const float MaximumDailyCalories = 4000f;
    private const float CalorieRoundingIncrement = 10f;

    private readonly ICalorieCalculator _calorieCalculator;

    /// <summary>Takes the calorie maths as a dependency, so the targets can be recomputed without the planner knowing how.</summary>
    public NutritionPlanner(ICalorieCalculator calorieCalculator)
    {
        _calorieCalculator = calorieCalculator;
    }

    /// <summary>Builds the day's nutrition plan from the profile and its activity.</summary>
    public NutritionPlan CreateDailyPlan(
        UserProfile profile,
        CyclePhase phase,
        DateTime date,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        if (!HasUsableMetrics(profile, date))
            return CreateFallbackPlan(phase, breakfastPreference);

        var age = CalculateAge(profile.DateOfBirth, date);
        var bmr = _calorieCalculator.CalculateBmr(profile.Weight, profile.Height, age);
        var activityMultiplier = CalculateActivityMultiplier(
            profile.WorkoutDaysPerWeek,
            profile.TrainingExperienceLevel);
        var tdee = bmr * activityMultiplier;
        var reproductiveContext = ReproductiveContextCodec.Parse(profile.ReproductiveContextJson);
        var targetPhase = NutritionTargetPhaseRule.ForAutomaticTargets(phase, reproductiveContext);
        var goalAdjustment = CalculateGoalAdjustment(
            tdee,
            profile.Goal,
            profile.WeightGoalPace);
        if (!reproductiveContext.AllowsCalorieDeficit && goalAdjustment < 0f)
            goalAdjustment = 0f;

        var phaseAdjustment = NutritionTargetPhaseRule.CalculateExistingCalorieAdjustment(targetPhase, profile.Goal);
        var calories = RoundToNearest(
            _calorieCalculator.Clamp(
                tdee + goalAdjustment + phaseAdjustment),
            CalorieRoundingIncrement);
        var calculatedCalories = calories;
        var macros = CalculateMacros(calories, profile.Weight, profile.Goal, targetPhase);
        var fiber = CalculateFiber(calories, targetPhase);
        var water = CalculateWaterLiters(profile.Weight, profile.WorkoutDaysPerWeek, targetPhase);
        var calculatedGoals = ProfileNutritionGoals.FromCalculated(
            calories,
            macros.Protein,
            macros.Carbs,
            macros.Fats,
            fiber,
            water);
        var customGoals = ProfileNutritionGoalsJsonCodec.FromJson(profile.NutritionGoalsJson);
        var goals = customGoals.HasAnyValue
            ? customGoals.WithFallbacks(calculatedGoals)
            : calculatedGoals;
        calories = goals.Calories ?? calories;
        macros = (
            goals.Protein ?? macros.Protein,
            goals.Carbs ?? macros.Carbs,
            goals.Fats ?? macros.Fats);
        var safeCalories = Math.Clamp(calories, MinimumDailyCalories, MaximumDailyCalories);
        var savedMacroOverridesRetained = safeCalories == calories;
        if (safeCalories != calories)
        {
            calories = safeCalories;
            macros = CalculateMacros(calories, profile.Weight, profile.Goal, targetPhase);
            goals = goals with
            {
                Calories = calories,
                Protein = macros.Protein,
                Carbs = macros.Carbs,
                Fats = macros.Fats
            };
        }

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
            targetPhase,
            GetPhaseFocus(targetPhase),
            BuildNotes(profile),
            goals,
            BuildMealTargets(calories, macros.Protein, macros.Carbs, macros.Fats, breakfastPreference),
            breakfastPreference,
            NutritionTargetProvenance.Calculated(tdee, goalAdjustment, phaseAdjustment,
                calculatedCalories, calories, customGoals, calculatedGoals, savedMacroOverridesRetained));
    }

    /// <summary>Builds the plan used when the personalised calculation cannot run.</summary>
    public NutritionPlan CreateFallbackPlan(
        CyclePhase phase,
        BreakfastPreference breakfastPreference = BreakfastPreference.Savoury)
    {
        var targetPhase = NutritionTargetPhaseRule.ForAutomaticTargets(phase, ReproductiveContext.Default);
        return new NutritionPlan(
            DefaultCalories,
            DefaultProtein,
            DefaultCarbs,
            DefaultFats,
            DefaultFiberGrams,
            DefaultWaterLiters,
            0f,
            0f,
            0f,
            0f,
            0f,
            targetPhase,
            GetPhaseFocus(targetPhase),
            ["Complete profile setup to personalize targets."],
            ProfileNutritionGoals.FromCalculated(DefaultCalories, DefaultProtein, DefaultCarbs, DefaultFats,
                DefaultFiberGrams, DefaultWaterLiters),
            BuildMealTargets(DefaultCalories, DefaultProtein, DefaultCarbs, DefaultFats, breakfastPreference),
            breakfastPreference,
            NutritionTargetProvenance.GeneralStarting(DefaultCalories));
    }

    /// <summary>Whether the profile carries enough measurements to compute a target at all.</summary>
    private static bool HasUsableMetrics(UserProfile profile, DateTime date)
    {
        return float.IsFinite(profile.Height) &&
               float.IsFinite(profile.Weight) &&
               profile.Height is >= 100f and <= 300f &&
               profile.Weight is >= 30f and <= 500f &&
               profile.DateOfBirth.Year > 1900 &&
               profile.DateOfBirth.Date <= date.Date &&
               CalculateAge(profile.DateOfBirth, date) >= 18;
    }

    /// <summary>Age in whole years from a birth date, which the basal rate needs.</summary>
    public static int CalculateAge(DateTime dateOfBirth, DateTime date)
    {
        var age = date.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > date.Date.AddYears(-age))
            age--;

        return Math.Clamp(age, 12, 100);
    }

    /// <summary>The activity multiplier the profile's training frequency implies.</summary>
    private static float CalculateActivityMultiplier(
        int workoutDaysPerWeek,
        TrainingExperienceLevel experienceLevel)
    {
        var baseMultiplier = CalorieCalculator.GetActivityMultiplier(workoutDaysPerWeek);

        var experienceAdjustment = experienceLevel switch
        {
            TrainingExperienceLevel.Beginner => -0.025f,
            TrainingExperienceLevel.Advanced => 0.025f,
            _ => 0f
        };

        return Math.Clamp(baseMultiplier + experienceAdjustment, 1.2f, 1.85f);
    }

    /// <summary>The calorie change the profile's goal applies.</summary>
    private static float CalculateGoalAdjustment(
        float tdee,
        UserGoal goal,
        WeightGoalPace pace)
    {
        var isAggressive = pace is WeightGoalPace.Aggressive;

        return goal switch
        {
            UserGoal.FatLoss => -tdee * (isAggressive ? 0.22f : 0.12f),
            UserGoal.Strength => MathF.Min(tdee * (isAggressive ? 0.12f : 0.08f), isAggressive ? 400f : 250f),
            UserGoal.MuscleTone => MathF.Min(tdee * 0.04f, 150f),
            _ => 0f
        };
    }

    /// <summary>The protein, carb and fat grams the plan targets.</summary>
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

    /// <summary>The fibre target, scaled from the calorie target and the phase's focus.</summary>
    private static float CalculateFiber(float calories, CyclePhase phase)
    {
        var baseline = MathF.Max(25f, calories / 1000f * 14f);
        var phaseBonus = phase is CyclePhase.Luteal ? 3f : 0f;
        return RoundToNearest(baseline + phaseBonus, 1f);
    }

    /// <summary>Litres of water the plan recommends for the day.</summary>
    private static float CalculateWaterLiters(
        float weightKg,
        int workoutDaysPerWeek,
        CyclePhase phase)
    {
        var trainingBonus = Math.Clamp(workoutDaysPerWeek, 0, 7) * 0.1f;
        var phaseBonus = phase is CyclePhase.Luteal or CyclePhase.Ovulatory ? 0.2f : 0f;
        return RoundToNearest(Math.Clamp(weightKg * 0.035f + trainingBonus + phaseBonus, 1.8f, 4.0f), 0.1f);
    }

    /// <summary>What the phase is asking of the diet, in the words the plan shows.</summary>
    private static string GetPhaseFocus(CyclePhase phase)
    {
        return phase switch
        {
            CyclePhase.Menstrual => "Prioritize iron, protein, warm meals, and steady hydration.",
            CyclePhase.Follicular => "Use the rising-energy window for balanced protein and training carbs.",
            CyclePhase.Ovulatory => "Support peak output with protein, carbs, and extra fluids.",
            CyclePhase.Luteal => "Plan filling carbs, fiber, magnesium-rich foods, and steady snacks.",
            _ => "Keep meals balanced and consistent."
        };
    }

    /// <summary>The nutrition notes for a phase, which the plan shows alongside the targets.</summary>
    private static IReadOnlyCollection<string> BuildNotes(UserProfile profile)
    {
        List<string> notes = [];

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

    /// <summary>How the day's calories split across meals for the phase.</summary>
    public static MealDistributionShares GetMealShares(BreakfastPreference preference)
    {
        var shares = MealDistributionRule.GetShares(preference);
        return new MealDistributionShares(shares.Breakfast, shares.Lunch,
            shares.Dinner, shares.Snack);
    }

    /// <summary>One meal's share of the day's calories.</summary>
    public static float GetMealShare(MealType mealType, BreakfastPreference preference)
    {
        var shares = GetMealShares(preference);
        return mealType switch
        {
            MealType.Breakfast => shares.Breakfast,
            MealType.Lunch => shares.Lunch,
            MealType.Dinner => shares.Dinner,
            MealType.Snack => shares.Snack,
            _ => 0f
        };
    }

    /// <summary>One meal's share of the day's protein, which can differ from its calorie share.</summary>
    public static float GetMealProteinShare(MealType mealType, BreakfastPreference preference)
    {
        return GetMealShares(preference).GetProteinShare(mealType);
    }

    // Meal targets allocate 34% calories to lunch (within standard 30-35% peak fueling range),
    // 26-30% for breakfast and dinner based on morning preference, and 10% for a flexible snack.
    // Daily protein goals are distributed 100% across the three main meals (Breakfast, Lunch, Dinner)
    // to preserve substantial meal protein thresholds, leaving snacks protein-neutral.
    /// <summary>Splits the daily targets across the day's meals.</summary>
    public static IReadOnlyCollection<MealNutritionTarget> BuildMealTargets(
        float calories,
        float protein,
        float carbs,
        float fats,
        BreakfastPreference breakfastPreference)
    {
        var shares = GetMealShares(breakfastPreference);

        return
        [
            BuildMealTarget(MealType.Breakfast, shares.Breakfast, shares.GetProteinShare(MealType.Breakfast), calories,
                protein, carbs, fats),
            BuildMealTarget(MealType.Lunch, shares.Lunch, shares.GetProteinShare(MealType.Lunch), calories, protein,
                carbs, fats),
            BuildMealTarget(MealType.Dinner, shares.Dinner, shares.GetProteinShare(MealType.Dinner), calories, protein,
                carbs, fats),
            BuildMealTarget(MealType.Snack, shares.Snack, shares.GetProteinShare(MealType.Snack), calories, protein,
                carbs, fats)
        ];
    }

    /// <summary>The targets for one meal of the day.</summary>
    public static MealNutritionTarget BuildMealTarget(
        MealType mealType,
        float calorieShare,
        float proteinShare,
        float calories,
        float protein,
        float carbs,
        float fats)
    {
        return new MealNutritionTarget(
            mealType,
            RoundToNearest(calories * calorieShare, 10f),
            RoundToNearest(protein * proteinShare, 1f),
            RoundToNearest(carbs * calorieShare, 1f),
            RoundToNearest(fats * calorieShare, 1f));
    }

    /// <summary>The targets for one meal with an explicit calorie share.</summary>
    public static MealNutritionTarget BuildMealTarget(
        MealType mealType,
        float share,
        float calories,
        float protein,
        float carbs,
        float fats)
    {
        return BuildMealTarget(mealType, share, share, calories, protein, carbs, fats);
    }

    /// <summary>A value rounded to the step the plan displays in, so the numbers read as chosen rather than computed.</summary>
    public static float RoundToNearest(float value, float nearest)
    {
        return MathF.Round(value / nearest) * nearest;
    }

    /// <summary>The shares of the day each meal receives.</summary>
    public readonly record struct MealDistributionShares(
        float Breakfast,
        float Lunch,
        float Dinner,
        float Snack = 0.10f)
    {
        /// <summary>The share left for the main meals once the smaller ones are taken.</summary>
        public float MainMealTotal => Breakfast + Lunch + Dinner;

        /// <summary>A meal's protein share, which is the one the plan uses to size its protein portion.</summary>
        public float GetProteinShare(MealType mealType)
        {
            var mainTotal = MainMealTotal;
            if (mainTotal <= 0f) return 0f;

            return mealType switch
            {
                MealType.Breakfast => Breakfast / mainTotal,
                MealType.Lunch => Lunch / mainTotal,
                MealType.Dinner => Dinner / mainTotal,
                _ => 0f
            };
        }
    }
}
