namespace MuscleCuties.Core.Domain.Nutrition;

public sealed record PortionObservation(
    float Calories,
    float Protein,
    float Carbs,
    float Fats,
    float ServingGrams = 0f
);

public sealed record PortionResult(
    float ProteinGrams,
    float CarbGrams,
    float VegGrams,
    float SauceGrams,
    float TotalCalories,
    float TotalProtein,
    float TotalCarbs,
    float TotalFats
);

public static class PortionCalculationRule
{
    private const int FatLossGoal = 0;
    private const float GramsPerHundred = 100f;
    private const float KcalTolerancePct = 0.15f;
    private const float RelaxedKcalTolerancePct = 0.25f;
    private const float ProteinDeficitToleranceG = 8f;
    private const float DeficitTieToleranceG = 0.001f;

    private const float AnchorProteinCalorieThresholdKcal = 300f;
    private const float HighCalorieAnchorProteinGrams = 12f;
    private const float LowCalorieAnchorProteinGrams = 8f;

    private const float BaseVegetableGrams = 120f;
    private const float MinVegetableGrams = 60f;
    private const float MaxVegetableGrams = 300f;
    private const float RestDayVegetableMultiplier = 1.5f;
    private const float FatLossVegetableMultiplier = 1.3f;

    private const float MinCarbGrams = 30f;
    private const float MaxCarbGrams = 180f;
    private const float RelaxedMaxCarbGrams = 220f;
    private const float DefaultCarbGrams = 100f;

    private const float MinPortionGrams = 50f;
    private const float MaxPortionGrams = 300f;
    private const float PortionSnapGrams = 5f;
    private const float MaxSauceGrams = 30f;

    public static PortionResult? Solve(
        float targetCalories,
        float targetProtein,
        float targetCarbs,
        float targetFats,
        PortionObservation protein,
        PortionObservation carb,
        PortionObservation veg,
        PortionObservation? sauce,
        int userGoal, // 0 = FatLoss
        bool isRestDay,
        bool relaxed = false)
    {
        if (!IsValidTarget(targetCalories, targetProtein, targetCarbs, targetFats, protein, carb))
            return null;

        var vegMacros = CalculateVegetablePortion(veg, userGoal, isRestDay);
        var carbMacros = CalculateCarbPortion(carb, targetCarbs, vegMacros.Carbs, relaxed);

        var remainingProteinG = MathF.Max(targetProtein - vegMacros.Protein - carbMacros.Protein, 0f);
        var minimumAnchorProtein = targetCalories >= AnchorProteinCalorieThresholdKcal
            ? HighCalorieAnchorProteinGrams
            : LowCalorieAnchorProteinGrams;
        var rawProteinGrams = MathF.Max(
            remainingProteinG / protein.Protein * GramsPerHundred,
            minimumAnchorProtein / protein.Protein * GramsPerHundred);

        var candidateGrams = GenerateProteinCandidateGrams(protein, rawProteinGrams);
        var kcalTolerance = relaxed ? RelaxedKcalTolerancePct : KcalTolerancePct;

        return EvaluateCandidateConvergence(
            candidateGrams,
            protein,
            carbMacros,
            vegMacros,
            sauce,
            targetCalories,
            targetProtein,
            targetFats,
            minimumAnchorProtein,
            kcalTolerance);
    }

    private static bool IsValidTarget(
        float targetCalories,
        float targetProtein,
        float targetCarbs,
        float targetFats,
        PortionObservation protein,
        PortionObservation carb)
    {
        return float.IsFinite(targetCalories) &&
               float.IsFinite(targetProtein) &&
               float.IsFinite(targetCarbs) &&
               float.IsFinite(targetFats) &&
               targetCalories > 0f &&
               targetProtein > 0f &&
               protein.Protein > 0f &&
               carb.Carbs > 0f;
    }

    private static PortionComponentMacros CalculateVegetablePortion(
        PortionObservation veg,
        int userGoal,
        bool isRestDay)
    {
        var vegMultiplier = (isRestDay ? RestDayVegetableMultiplier : 1f) *
                            (userGoal == FatLossGoal ? FatLossVegetableMultiplier : 1f);
        var vegGrams = Math.Clamp(BaseVegetableGrams * vegMultiplier, MinVegetableGrams, MaxVegetableGrams);
        return new PortionComponentMacros(
            vegGrams,
            veg.Calories * vegGrams / GramsPerHundred,
            veg.Protein * vegGrams / GramsPerHundred,
            veg.Carbs * vegGrams / GramsPerHundred,
            veg.Fats * vegGrams / GramsPerHundred);
    }

    private static PortionComponentMacros CalculateCarbPortion(
        PortionObservation carb,
        float targetCarbs,
        float vegCarbs,
        bool relaxed)
    {
        var remainingCarbG = MathF.Max(targetCarbs - vegCarbs, 0f);
        var maxCarbGrams = relaxed ? RelaxedMaxCarbGrams : MaxCarbGrams;
        var carbGrams = carb.Carbs > 0f
            ? Math.Clamp(remainingCarbG / carb.Carbs * GramsPerHundred, MinCarbGrams, maxCarbGrams)
            : DefaultCarbGrams;
        carbGrams = SnapToServing(carbGrams, carb.ServingGrams);

        return new PortionComponentMacros(
            carbGrams,
            carb.Calories * carbGrams / GramsPerHundred,
            carb.Protein * carbGrams / GramsPerHundred,
            carb.Carbs * carbGrams / GramsPerHundred,
            carb.Fats * carbGrams / GramsPerHundred);
    }

    private static float[] GenerateProteinCandidateGrams(
        PortionObservation protein,
        float rawProteinGrams)
    {
        var servingGrams = protein.ServingGrams;
        float downCandidate;
        float upCandidate;

        if (servingGrams > 0f)
        {
            var minServings = MathF.Max(1f, MathF.Ceiling(MinPortionGrams / servingGrams));
            var maxServings = MathF.Max(minServings, MathF.Floor(MaxPortionGrams / servingGrams));

            var floorServings = Math.Clamp(MathF.Floor(rawProteinGrams / servingGrams), minServings, maxServings);
            var ceilServings = Math.Clamp(MathF.Ceiling(rawProteinGrams / servingGrams), minServings, maxServings);

            downCandidate = Math.Clamp(floorServings * servingGrams, MinPortionGrams, MaxPortionGrams);
            upCandidate = Math.Clamp(ceilServings * servingGrams, MinPortionGrams, MaxPortionGrams);
        }
        else
        {
            downCandidate = Math.Clamp(MathF.Floor(rawProteinGrams / PortionSnapGrams) * PortionSnapGrams,
                MinPortionGrams, MaxPortionGrams);
            upCandidate = Math.Clamp(MathF.Ceiling(rawProteinGrams / PortionSnapGrams) * PortionSnapGrams,
                MinPortionGrams, MaxPortionGrams);
        }

        return downCandidate.Equals(upCandidate)
            ? [downCandidate]
            : [downCandidate, upCandidate];
    }

    private static PortionComponentMacros CalculateSaucePortion(
        PortionObservation? sauce,
        float targetFats,
        float currentFats)
    {
        if (sauce is null || sauce.Fats <= 0f)
            return default;

        var remainingFatG = MathF.Max(targetFats - currentFats, 0f);
        var rawSauceGrams = Math.Clamp(remainingFatG / sauce.Fats * GramsPerHundred, 0f, MaxSauceGrams);
        if (rawSauceGrams <= 0f)
            return default;

        var sauceGrams = SnapToServing(rawSauceGrams, sauce.ServingGrams);
        return new PortionComponentMacros(
            sauceGrams,
            sauce.Calories * sauceGrams / GramsPerHundred,
            sauce.Protein * sauceGrams / GramsPerHundred,
            sauce.Carbs * sauceGrams / GramsPerHundred,
            sauce.Fats * sauceGrams / GramsPerHundred);
    }

    private static PortionResult? EvaluateCandidateConvergence(
        float[] candidateGrams,
        PortionObservation protein,
        PortionComponentMacros carb,
        PortionComponentMacros veg,
        PortionObservation? sauce,
        float targetCalories,
        float targetProtein,
        float targetFats,
        float minimumAnchorProtein,
        float kcalTolerance)
    {
        PortionResult? bestSolution = null;
        var bestEffectiveDeficit = float.MaxValue;
        var bestCalorieError = float.MaxValue;

        foreach (var pGrams in candidateGrams)
        {
            var pProtein = protein.Protein * pGrams / GramsPerHundred;
            if (pProtein < minimumAnchorProtein)
                continue;

            var pCarbs = protein.Carbs * pGrams / GramsPerHundred;
            var pFats = protein.Fats * pGrams / GramsPerHundred;
            var pCalories = protein.Calories * pGrams / GramsPerHundred;

            var sauceMacros = CalculateSaucePortion(sauce, targetFats, pFats + carb.Fats + veg.Fats);

            var totalCalories = pCalories + carb.Calories + veg.Calories + sauceMacros.Calories;
            var totalProtein = pProtein + carb.Protein + veg.Protein + sauceMacros.Protein;
            var totalCarbs = pCarbs + carb.Carbs + veg.Carbs + sauceMacros.Carbs;
            var totalFats = pFats + carb.Fats + veg.Fats + sauceMacros.Fats;

            if (targetCalories > 0f &&
                MathF.Abs(totalCalories - targetCalories) / targetCalories > kcalTolerance)
                continue;

            var proteinDeficit = targetProtein - totalProtein;
            if (proteinDeficit > ProteinDeficitToleranceG) continue;

            var effectiveDeficit = MathF.Max(0f, proteinDeficit);
            var calorieError = targetCalories > 0f ? MathF.Abs(totalCalories - targetCalories) : 0f;

            if (bestSolution is null ||
                effectiveDeficit < bestEffectiveDeficit ||
                (MathF.Abs(effectiveDeficit - bestEffectiveDeficit) < DeficitTieToleranceG &&
                 calorieError < bestCalorieError))
            {
                bestSolution = new PortionResult(
                    pGrams, carb.Grams, veg.Grams, sauceMacros.Grams,
                    totalCalories, totalProtein, totalCarbs, totalFats);
                bestEffectiveDeficit = effectiveDeficit;
                bestCalorieError = calorieError;
            }
        }

        return bestSolution;
    }

    public static float SnapToServing(float targetGrams, float servingGrams)
    {
        if (servingGrams <= 0f)
            return MathF.Round(targetGrams / PortionSnapGrams) * PortionSnapGrams;

        var servings = MathF.Max(1f, MathF.Round(targetGrams / servingGrams));
        return servings * servingGrams;
    }

    private readonly record struct PortionComponentMacros(
        float Grams,
        float Calories,
        float Protein,
        float Carbs,
        float Fats);
}
