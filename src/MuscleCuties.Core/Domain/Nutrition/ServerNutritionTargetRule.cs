using MuscleCuties.Core.Domain.Health;

using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Domain.Nutrition;

public sealed record ServerNutritionTargetResult(
    float Calories, float Protein, float Carbs, float Fats,
    NutritionTargetProvenance Provenance);

/// <summary>Deterministic target math for the server projection. Persistence and profile decoding stay outside Core.</summary>
public static class ServerNutritionTargetRule
{
    public const float MinimumCalories = 1200f;
    public const float MaximumCalories = 4000f;
    public const float GeneralCalories = 2000f;
    public const float GeneralProtein = 120f;
    public const float GeneralCarbs = 200f;
    public const float GeneralFats = 65f;
    private const float CalorieRoundingIncrement = 10f;
    private const float BmrWeightFactor = 10f;
    private const float BmrHeightFactor = 6.25f;
    private const float BmrAgeFactor = 5f;
    private const float BmrFemaleAdjustment = 161f;
    private const float MinimumActivityMultiplier = 1.2f;
    private const float MaximumActivityMultiplier = 1.85f;
    private const float BeginnerActivityAdjustment = -0.025f;
    private const float AdvancedActivityAdjustment = 0.025f;
    private const float SteadyFatLossAdjustment = 0.12f;
    private const float AggressiveFatLossAdjustment = 0.20f;
    private const float MaximumAggressiveFatLossAdjustmentCalories = 500f;
    private const float SteadyStrengthAdjustment = 0.08f;
    private const float AggressiveStrengthAdjustment = 0.12f;
    private const float MaximumSteadyStrengthAdjustment = 250f;
    private const float MaximumAggressiveStrengthAdjustment = 400f;
    private const float MuscleToneAdjustment = 0.04f;
    private const float MaximumMuscleToneAdjustment = 150f;
    private const float DefaultWaterLiters = 2.3f;
    private const float MinimumFiberGrams = 25f;
    private const float FiberGramsPerThousandCalories = 14f;
    private const float CaloriesPerThousand = 1000f;
    private const float MinimumCarbohydrateCalorieRatio = 0.25f;
    private const float MinimumFatWeightRatio = 0.7f;
    private const float AdjustedMinimumFatWeightRatio = 0.6f;
    private const int LowActivityDaysThreshold = 2;
    private const int ModerateActivityDaysThreshold = 4;
    private const int HighActivityDaysThreshold = 6;
    private const float LowActivityMultiplier = 1.375f;
    private const float ModerateActivityMultiplier = 1.55f;
    private const float HighActivityMultiplier = 1.725f;
    private const float MaximumActivityMultiplierValue = 1.8f;
    private const float FatLossProteinGramsPerKg = 2f;
    private const float StrengthProteinGramsPerKg = 1.9f;
    private const float MuscleToneProteinGramsPerKg = 1.8f;
    private const float DefaultProteinGramsPerKg = 1.6f;
    private const float CaloriesPerGramProtein = 4f;
    private const float CaloriesPerGramFat = 9f;
    private const float CycleLutealFatPercent = 0.30f;
    private const float OtherCycleFatPercent = 0.25f;

    /// <summary>Conservative projection when persisted goal data is not a supported value.</summary>
    public static ServerNutritionTargetResult Unavailable() => new(
        GeneralCalories, GeneralProtein, GeneralCarbs, GeneralFats,
        NutritionTargetProvenance.Unavailable(GeneralCalories));

    public static ServerNutritionTargetResult Calculate(int age, float heightCm, float weightKg,
        int workoutDaysPerWeek, UserGoal goal, WeightGoalPace pace, string trainingExperience,
        CyclePhase phase, ReproductiveContext context, ProfileNutritionGoals saved)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(saved);
        if (age < 18 || !float.IsFinite(heightCm) || !float.IsFinite(weightKg) ||
            heightCm < 100f || heightCm > 300f || weightKg < 30f || weightKg > 500f ||
            !Enum.IsDefined(goal) || !Enum.IsDefined(pace))
            throw new ArgumentOutOfRangeException(nameof(weightKg));

        var experience = string.Equals(trainingExperience, "Beginner", StringComparison.OrdinalIgnoreCase) ? BeginnerActivityAdjustment :
            string.Equals(trainingExperience, "Advanced", StringComparison.OrdinalIgnoreCase) ? AdvancedActivityAdjustment : 0f;
        var bmr = BmrWeightFactor * weightKg + BmrHeightFactor * heightCm - BmrAgeFactor * age - BmrFemaleAdjustment;
        var multiplier = Math.Clamp(ActivityMultiplier(workoutDaysPerWeek) + experience, MinimumActivityMultiplier, MaximumActivityMultiplier);
        var tdee = bmr * multiplier;
        var goalAdjustment = goal switch
        {
            UserGoal.FatLoss => pace == WeightGoalPace.Aggressive
                ? -MathF.Min(tdee * AggressiveFatLossAdjustment, MaximumAggressiveFatLossAdjustmentCalories)
                : -tdee * SteadyFatLossAdjustment,
            UserGoal.Strength => MathF.Min(tdee * (pace == WeightGoalPace.Aggressive ? AggressiveStrengthAdjustment : SteadyStrengthAdjustment), pace == WeightGoalPace.Aggressive ? MaximumAggressiveStrengthAdjustment : MaximumSteadyStrengthAdjustment),
            UserGoal.MuscleTone => MathF.Min(tdee * MuscleToneAdjustment, MaximumMuscleToneAdjustment),
            _ => 0f
        };
        if (!context.AllowsCalorieDeficit && goalAdjustment < 0f) goalAdjustment = 0f;
        var phaseAdjustment = NutritionTargetPhaseRule.CalculateExistingCalorieAdjustment(phase, goal);
        var calculated = MathF.Round(Math.Clamp(tdee + goalAdjustment + phaseAdjustment, MinimumCalories, MaximumCalories) / CalorieRoundingIncrement) * CalorieRoundingIncrement;
        var (protein, carbs, fats) = CalculateMacros(calculated, weightKg, goal, phase);
        var calculatedGoals = ProfileNutritionGoals.FromCalculated(calculated, protein, carbs, fats,
            MathF.Max(MinimumFiberGrams, calculated / CaloriesPerThousand * FiberGramsPerThousandCalories), DefaultWaterLiters);
        var rawCalories = saved.Calories ?? calculated;
        var shownCalories = Math.Clamp(rawCalories, MinimumCalories, MaximumCalories);
        var macrosRetained = shownCalories == rawCalories;
        if (!macrosRetained) (protein, carbs, fats) = CalculateMacros(shownCalories, weightKg, goal, phase);
        protein = macrosRetained ? saved.Protein ?? protein : protein;
        carbs = macrosRetained ? saved.Carbs ?? carbs : carbs;
        fats = macrosRetained ? saved.Fats ?? fats : fats;
        var provenance = NutritionTargetProvenance.Calculated(tdee, goalAdjustment, phaseAdjustment,
            calculated, shownCalories, saved, calculatedGoals, macrosRetained);
        return new(shownCalories, protein, carbs, fats, provenance);
    }

    private static float ActivityMultiplier(int days) => days switch { <= 0 => MinimumActivityMultiplier, <= LowActivityDaysThreshold => LowActivityMultiplier, <= ModerateActivityDaysThreshold => ModerateActivityMultiplier, <= HighActivityDaysThreshold => HighActivityMultiplier, _ => MaximumActivityMultiplierValue };
    private static (float Protein, float Carbs, float Fats) CalculateMacros(float calories, float weight, UserGoal goal, CyclePhase phase)
    {
        var proteinPerKg = goal switch { UserGoal.FatLoss => FatLossProteinGramsPerKg, UserGoal.Strength => StrengthProteinGramsPerKg, UserGoal.MuscleTone => MuscleToneProteinGramsPerKg, _ => DefaultProteinGramsPerKg };
        var protein = MathF.Round(weight * proteinPerKg);
        var fatPercent = phase is CyclePhase.Menstrual or CyclePhase.Luteal ? CycleLutealFatPercent : OtherCycleFatPercent;
        var fats = MathF.Max(weight * MinimumFatWeightRatio, calories * fatPercent / CaloriesPerGramFat);
        var minimumCarbCalories = calories * MinimumCarbohydrateCalorieRatio;
        var caloriesAfterProtein = calories - protein * CaloriesPerGramProtein;
        if (caloriesAfterProtein - fats * CaloriesPerGramFat < minimumCarbCalories)
            fats = MathF.Max(weight * AdjustedMinimumFatWeightRatio, (caloriesAfterProtein - minimumCarbCalories) / CaloriesPerGramFat);
        fats = MathF.Round(MathF.Max(fats, 0f));
        var carbs = MathF.Round(MathF.Max((calories - protein * CaloriesPerGramProtein - fats * CaloriesPerGramFat) / CaloriesPerGramProtein, 0f));
        return (protein, carbs, fats);
    }
}
