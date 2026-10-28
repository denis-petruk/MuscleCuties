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
    public const float MaxOilGrams = 14f; // Generated oil ceiling.
    private const float OilFatDensityThreshold = 80f;
    private const float DefaultSauceMaxGrams = 30f;
    private const float PortionStepGrams = 5f;
    private const float MinimumProteinGrams = 50f;
    private const float MaximumProteinGrams = 300f;
    private const float GramsPerHundred = 100f;
    private const float KcalTolerancePct = 0.15f;
    private const float ProteinDeficitToleranceG = 8f;
    private const float RelaxedKcalTolerancePct = 0.25f;
    private const float MinCarbGrams = 30f;
    private const float MaxCarbGrams = 180f;
    private const float RelaxedMaxCarbGrams = 220f;

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
        var minimumAnchorProtein = targetCalories >= 300f ? 12f : 8f;
        var rawProteinGrams = MathF.Max(
            remainingProteinG / protein.Protein * 100f,
            minimumAnchorProtein / protein.Protein * 100f);

        var candidateGrams = GenerateProteinCandidateGrams(protein, rawProteinGrams);
        var kcalTolerance = relaxed ? RelaxedKcalTolerancePct : KcalTolerancePct;

        var result = EvaluateCandidateConvergence(
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

        if (result is not null || sauce is null || sauce.Fats < OilFatDensityThreshold)
            return result;

        // The oil cap can leave the original whole-food portions short of calories.
        // Search other serving-sized portions while retaining the existing acceptance rules.
        var proteinCandidates = GenerateRebalanceCandidates(
            MinimumProteinGrams, MaximumProteinGrams, protein.ServingGrams);
        var carbCandidates = GenerateRebalanceCandidates(
            MinCarbGrams, relaxed ? RelaxedMaxCarbGrams : MaxCarbGrams, carb.ServingGrams);
        PortionResult? best = null;
        foreach (var carbGrams in carbCandidates)
        {
            var adjustedCarb = new PortionComponentMacros(
                carbGrams, carb.Calories * carbGrams / GramsPerHundred,
                carb.Protein * carbGrams / GramsPerHundred,
                carb.Carbs * carbGrams / GramsPerHundred,
                carb.Fats * carbGrams / GramsPerHundred);
            var candidate = EvaluateCandidateConvergence(
                proteinCandidates, protein, adjustedCarb, vegMacros, sauce,
                targetCalories, targetProtein, targetFats, minimumAnchorProtein, kcalTolerance);
            if (candidate is not null && (best is null ||
                MathF.Abs(candidate.TotalCalories - targetCalories) < MathF.Abs(best.TotalCalories - targetCalories)))
                best = candidate;
        }

        return best;
    }

    private static float[] GenerateRebalanceCandidates(float minimum, float maximum, float servingGrams)
    {
        var step = servingGrams > 0f
            ? servingGrams * MathF.Max(1f, MathF.Ceiling(PortionStepGrams / servingGrams))
            : PortionStepGrams;
        var first = MathF.Ceiling(minimum / step);
        var last = MathF.Floor(maximum / step);
        var candidates = new List<float>();
        for (var serving = first; serving <= last; serving++)
            candidates.Add(serving * step);
        return candidates.ToArray();
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
        var vegMultiplier = (isRestDay ? 1.5f : 1f) * (userGoal == 0 ? 1.3f : 1f);
        var vegGrams = Math.Clamp(120f * vegMultiplier, 60f, 300f);
        return new PortionComponentMacros(
            vegGrams,
            veg.Calories * vegGrams / 100f,
            veg.Protein * vegGrams / 100f,
            veg.Carbs * vegGrams / 100f,
            veg.Fats * vegGrams / 100f);
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
            ? Math.Clamp(remainingCarbG / carb.Carbs * 100f, MinCarbGrams, maxCarbGrams)
            : 100f;
        carbGrams = SnapToServing(carbGrams, carb.ServingGrams);

        return new PortionComponentMacros(
            carbGrams,
            carb.Calories * carbGrams / 100f,
            carb.Protein * carbGrams / 100f,
            carb.Carbs * carbGrams / 100f,
            carb.Fats * carbGrams / 100f);
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
            var minServings = MathF.Max(1f, MathF.Ceiling(50f / servingGrams));
            var maxServings = MathF.Max(minServings, MathF.Floor(300f / servingGrams));

            var floorServings = Math.Clamp(MathF.Floor(rawProteinGrams / servingGrams), minServings, maxServings);
            var ceilServings = Math.Clamp(MathF.Ceiling(rawProteinGrams / servingGrams), minServings, maxServings);

            downCandidate = Math.Clamp(floorServings * servingGrams, 50f, 300f);
            upCandidate = Math.Clamp(ceilServings * servingGrams, 50f, 300f);
        }
        else
        {
            downCandidate = Math.Clamp(MathF.Floor(rawProteinGrams / 5f) * 5f, 50f, 300f);
            upCandidate = Math.Clamp(MathF.Ceiling(rawProteinGrams / 5f) * 5f, 50f, 300f);
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
        var isHighFatOrOil = sauce.Fats >= OilFatDensityThreshold;
        var maxAllowedGrams = isHighFatOrOil ? MaxOilGrams : DefaultSauceMaxGrams;
        var rawSauceGrams = Math.Clamp(remainingFatG / sauce.Fats * 100f, 0f, maxAllowedGrams);
        if (rawSauceGrams <= 0f)
            return default;

        var snappedGrams = SnapToServing(rawSauceGrams, sauce.ServingGrams);
        var sauceGrams = isHighFatOrOil ? MathF.Min(snappedGrams, maxAllowedGrams) : snappedGrams;
        return new PortionComponentMacros(
            sauceGrams,
            sauce.Calories * sauceGrams / 100f,
            sauce.Protein * sauceGrams / 100f,
            sauce.Carbs * sauceGrams / 100f,
            sauce.Fats * sauceGrams / 100f);
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
            var pProtein = protein.Protein * pGrams / 100f;
            if (pProtein < minimumAnchorProtein)
                continue;

            var pCarbs = protein.Carbs * pGrams / 100f;
            var pFats = protein.Fats * pGrams / 100f;
            var pCalories = protein.Calories * pGrams / 100f;

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
                (MathF.Abs(effectiveDeficit - bestEffectiveDeficit) < 0.001f && calorieError < bestCalorieError))
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
            return MathF.Round(targetGrams / 5f) * 5f;

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
