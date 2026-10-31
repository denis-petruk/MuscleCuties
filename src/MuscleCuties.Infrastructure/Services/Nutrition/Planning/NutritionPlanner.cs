using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Infrastructure.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

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

    private const float UsableMinimumHeightCm = 100f;
    private const float UsableMaximumHeightCm = 300f;
    private const float UsableMinimumWeightKg = 30f;
    private const float UsableMaximumWeightKg = 500f;
    private const int MinimumBirthYear = 1900;
    private const int MinimumAge = 18;
    private const int AgeFloor = 12;
    private const int AgeCeiling = 100;

    private const float BeginnerExperienceAdjustment = -0.025f;
    private const float AdvancedExperienceAdjustment = 0.025f;
    private const float MinimumActivityMultiplier = 1.2f;
    private const float MaximumActivityMultiplier = 1.85f;

    private const float AggressiveFatLossRate = 0.22f;
    private const float StandardFatLossRate = 0.12f;
    private const float AggressiveStrengthRate = 0.12f;
    private const float StandardStrengthRate = 0.08f;
    private const float AggressiveStrengthCapKcal = 400f;
    private const float StandardStrengthCapKcal = 250f;
    private const float MuscleToneSurplusRate = 0.04f;
    private const float MuscleToneSurplusCapKcal = 150f;

    private const float FatLossProteinPerKg = 2.0f;
    private const float StrengthProteinPerKg = 1.9f;
    private const float MuscleToneProteinPerKg = 1.8f;
    private const float BalancedProteinPerKg = 1.6f;
    private const float MenstrualFatShare = 0.30f;
    private const float LutealFatShare = 0.30f;
    private const float StandardFatShare = 0.25f;
    private const float MinimumFatGramsPerKg = 0.7f;
    private const float MinimumFatFloorGramsPerKg = 0.6f;
    private const float MinimumCarbCalorieShare = 0.25f;
    private const float CaloriesPerGramProtein = 4f;
    private const float CaloriesPerGramCarb = 4f;
    private const float CaloriesPerGramFat = 9f;
    private const float WholeUnitRounding = 1f;

    private const float MinimumFiberGrams = 25f;
    private const float FiberGramsPerThousandKcal = 14f;
    private const float LutealFiberBonusGrams = 3f;
    private const float CaloriesPerThousand = 1000f;

    private const float WaterLitersPerKg = 0.035f;
    private const int MaximumWaterBonusTrainingDays = 7;
    private const float WaterLitersPerTrainingDay = 0.1f;
    private const float LutealAndOvulatoryWaterBonusLiters = 0.2f;
    private const float MinimumWaterLiters = 1.8f;
    private const float MaximumWaterLiters = 4.0f;
    private const float WaterRoundingLiters = 0.1f;

    private const float MealCalorieRoundingKcal = 10f;

    private readonly ICalorieCalculator _calorieCalculator;

    public NutritionPlanner(ICalorieCalculator calorieCalculator)
    {
        _calorieCalculator = calorieCalculator;
    }

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
            RoundToNearest(bmr, WholeUnitRounding),
            RoundToNearest(tdee, WholeUnitRounding),
            activityMultiplier,
            RoundToNearest(goalAdjustment, WholeUnitRounding),
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

    private static bool HasUsableMetrics(UserProfile profile, DateTime date)
    {
        return float.IsFinite(profile.Height) &&
               float.IsFinite(profile.Weight) &&
               profile.Height is >= UsableMinimumHeightCm and <= UsableMaximumHeightCm &&
               profile.Weight is >= UsableMinimumWeightKg and <= UsableMaximumWeightKg &&
               profile.DateOfBirth.Year > MinimumBirthYear &&
               profile.DateOfBirth.Date <= date.Date &&
               CalculateAge(profile.DateOfBirth, date) >= MinimumAge;
    }

    public static int CalculateAge(DateTime dateOfBirth, DateTime date)
    {
        var age = date.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > date.Date.AddYears(-age))
            age--;

        return Math.Clamp(age, AgeFloor, AgeCeiling);
    }

    private static float CalculateActivityMultiplier(
        int workoutDaysPerWeek,
        TrainingExperienceLevel experienceLevel)
    {
        var baseMultiplier = CalorieCalculator.GetActivityMultiplier(workoutDaysPerWeek);

        var experienceAdjustment = experienceLevel switch
        {
            TrainingExperienceLevel.Beginner => BeginnerExperienceAdjustment,
            TrainingExperienceLevel.Advanced => AdvancedExperienceAdjustment,
            _ => 0f
        };

        return Math.Clamp(baseMultiplier + experienceAdjustment, MinimumActivityMultiplier,
            MaximumActivityMultiplier);
    }

    private static float CalculateGoalAdjustment(
        float tdee,
        UserGoal goal,
        WeightGoalPace pace)
    {
        var isAggressive = pace is WeightGoalPace.Aggressive;

        return goal switch
        {
            UserGoal.FatLoss => -tdee * (isAggressive ? AggressiveFatLossRate : StandardFatLossRate),
            UserGoal.Strength => MathF.Min(
                tdee * (isAggressive ? AggressiveStrengthRate : StandardStrengthRate),
                isAggressive ? AggressiveStrengthCapKcal : StandardStrengthCapKcal),
            UserGoal.MuscleTone => MathF.Min(tdee * MuscleToneSurplusRate, MuscleToneSurplusCapKcal),
            _ => 0f
        };
    }

    private static (float Protein, float Carbs, float Fats) CalculateMacros(
        float calories,
        float weightKg,
        UserGoal goal,
        CyclePhase phase)
    {
        var proteinPerKg = goal switch
        {
            UserGoal.FatLoss => FatLossProteinPerKg,
            UserGoal.Strength => StrengthProteinPerKg,
            UserGoal.MuscleTone => MuscleToneProteinPerKg,
            _ => BalancedProteinPerKg
        };

        var protein = RoundToNearest(weightKg * proteinPerKg, WholeUnitRounding);
        var fatPercent = phase switch
        {
            CyclePhase.Menstrual => MenstrualFatShare,
            CyclePhase.Luteal => LutealFatShare,
            _ => StandardFatShare
        };

        var fats = MathF.Max(weightKg * MinimumFatGramsPerKg, calories * fatPercent / CaloriesPerGramFat);
        var minimumCarbCalories = calories * MinimumCarbCalorieShare;
        var caloriesAfterProtein = calories - protein * CaloriesPerGramProtein;

        if (caloriesAfterProtein - fats * CaloriesPerGramFat < minimumCarbCalories)
            fats = MathF.Max(weightKg * MinimumFatFloorGramsPerKg,
                (caloriesAfterProtein - minimumCarbCalories) / CaloriesPerGramFat);

        fats = RoundToNearest(MathF.Max(fats, 0f), WholeUnitRounding);
        var carbs = RoundToNearest(
            MathF.Max((calories - protein * CaloriesPerGramProtein - fats * CaloriesPerGramFat) /
                      CaloriesPerGramCarb, 0f), WholeUnitRounding);

        return (protein, carbs, fats);
    }

    private static float CalculateFiber(float calories, CyclePhase phase)
    {
        var baseline = MathF.Max(MinimumFiberGrams, calories / CaloriesPerThousand * FiberGramsPerThousandKcal);
        var phaseBonus = phase is CyclePhase.Luteal ? LutealFiberBonusGrams : 0f;
        return RoundToNearest(baseline + phaseBonus, WholeUnitRounding);
    }

    private static float CalculateWaterLiters(
        float weightKg,
        int workoutDaysPerWeek,
        CyclePhase phase)
    {
        var trainingBonus = Math.Clamp(workoutDaysPerWeek, 0, MaximumWaterBonusTrainingDays) *
                            WaterLitersPerTrainingDay;
        var phaseBonus = phase is CyclePhase.Luteal or CyclePhase.Ovulatory
            ? LutealAndOvulatoryWaterBonusLiters
            : 0f;
        return RoundToNearest(
            Math.Clamp(weightKg * WaterLitersPerKg + trainingBonus + phaseBonus,
                MinimumWaterLiters, MaximumWaterLiters), WaterRoundingLiters);
    }

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

    public static MealDistributionShares GetMealShares(BreakfastPreference preference)
    {
        var shares = MealDistributionRule.GetShares(preference);
        return new MealDistributionShares(shares.Breakfast, shares.Lunch,
            shares.Dinner, shares.Snack);
    }

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

    public static float GetMealProteinShare(MealType mealType, BreakfastPreference preference)
    {
        return GetMealShares(preference).GetProteinShare(mealType);
    }

    // Meal targets allocate 34% calories to lunch (within standard 30-35% peak fueling range),
    // 26-30% for breakfast and dinner based on morning preference, and 10% for a flexible snack.
    // Daily protein goals are distributed 100% across the three main meals (Breakfast, Lunch, Dinner)
    // to preserve substantial meal protein thresholds, leaving snacks protein-neutral.
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
            RoundToNearest(calories * calorieShare, MealCalorieRoundingKcal),
            RoundToNearest(protein * proteinShare, WholeUnitRounding),
            RoundToNearest(carbs * calorieShare, WholeUnitRounding),
            RoundToNearest(fats * calorieShare, WholeUnitRounding));
    }

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

    public static float RoundToNearest(float value, float nearest)
    {
        return MathF.Round(value / nearest) * nearest;
    }

    public readonly record struct MealDistributionShares(
        float Breakfast,
        float Lunch,
        float Dinner,
        float Snack)
    {
        public float MainMealTotal => Breakfast + Lunch + Dinner;

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
