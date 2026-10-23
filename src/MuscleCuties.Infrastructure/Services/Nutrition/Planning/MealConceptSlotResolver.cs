using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

internal sealed record ResolvedIngredientEntry(FoodItem Food, float PortionShare);

internal sealed record SlotFoodMap(
    IReadOnlyList<ResolvedIngredientEntry> Protein,
    IReadOnlyList<ResolvedIngredientEntry> Carb,
    IReadOnlyList<ResolvedIngredientEntry> Vitamin,
    IReadOnlyList<ResolvedIngredientEntry>? Sauce);

/// <summary>
///     Resolves meal concept slots to compatible food item entries.
/// </summary>
internal static class MealConceptSlotResolver
{
    public static SlotFoodMap? BuildSlotFoods(
        IReadOnlyList<MealConceptSlot> slots,
        IReadOnlyList<FoodItem> compatibleFoods)
    {
        List<ResolvedIngredientEntry>? carbEntries = null;
        List<ResolvedIngredientEntry>? proteinEntries = null;
        List<ResolvedIngredientEntry>? vitaminEntries = null;
        List<ResolvedIngredientEntry>? sauceEntries = null;

        foreach (var slot in slots)
        {
            var resolved = ResolveSlotEntries(compatibleFoods, slot.IngredientEntries, slot.SlotType);

            if (resolved.Count == 0 && slot.Required)
                return null;

            if (resolved.Count == 0)
                continue;

            switch (slot.SlotType)
            {
                case MealConceptSlotType.CarbBase:
                    carbEntries ??= resolved;
                    break;
                case MealConceptSlotType.ProteinBase:
                    proteinEntries ??= resolved;
                    break;
                case MealConceptSlotType.VitaminBase:
                    vitaminEntries ??= resolved;
                    break;
                case MealConceptSlotType.Sauce:
                    sauceEntries ??= resolved;
                    break;
            }
        }

        if (proteinEntries is null || carbEntries is null || vitaminEntries is null)
            return null;

        return new SlotFoodMap(proteinEntries, carbEntries, vitaminEntries, sauceEntries);
    }

    private static List<ResolvedIngredientEntry> ResolveSlotEntries(
        IReadOnlyList<FoodItem> compatibleFoods,
        IReadOnlyList<SlotIngredientEntry> entries,
        MealConceptSlotType slotType)
    {
        var resolved = new List<ResolvedIngredientEntry>();
        var usedIds = new HashSet<int>();

        foreach (var entry in entries)
        {
            var match = FindBestMatch(compatibleFoods, entry.PreferredFoodTerms, slotType, usedIds);

            if (match is null && entry.Required)
                match = FindByClassifier(compatibleFoods, slotType, usedIds, entry.PreferredFoodTerms);

            if (match is not null)
            {
                usedIds.Add(match.Id);
                resolved.Add(new ResolvedIngredientEntry(match, entry.PortionShare));
            }
            else if (entry.Required)
            {
                return [];
            }
        }

        return resolved;
    }

    private static FoodItem? FindBestMatch(
        IReadOnlyList<FoodItem> compatibleFoods,
        IReadOnlyList<string> preferredTerms,
        MealConceptSlotType slotType,
        IReadOnlySet<int> excludeIds)
    {
        foreach (var term in preferredTerms)
        {
            var candidates = compatibleFoods
                .Where(f => !excludeIds.Contains(f.Id) &&
                            f.Name.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                            IsFoodCompatibleWithSlot(f, slotType))
                .ToList();

            if (candidates.Count > 0)
                return candidates[Random.Shared.Next(candidates.Count)];
        }

        return null;
    }

    private static FoodItem? FindByClassifier(
        IReadOnlyList<FoodItem> compatibleFoods,
        MealConceptSlotType slotType,
        IReadOnlySet<int> excludeIds,
        IReadOnlyList<string>? preferredTerms = null)
    {
        return compatibleFoods.FirstOrDefault(f =>
            !excludeIds.Contains(f.Id) &&
            IsFoodCompatibleWithSlot(f, slotType) &&
            (slotType != MealConceptSlotType.CarbBase ||
             preferredTerms is null ||
             FoodComponentClassifier.IsCarbSemanticMatch(f, preferredTerms)));
    }

    private static bool IsFoodCompatibleWithSlot(FoodItem food, MealConceptSlotType slotType)
    {
        var componentType = FoodComponentClassifier.Classify(food);
        return slotType switch
        {
            MealConceptSlotType.ProteinBase => FoodComponentClassifier.IsProteinAnchor(food),
            MealConceptSlotType.CarbBase => FoodComponentClassifier.IsCarbohydrateBase(food),
            MealConceptSlotType.VitaminBase => componentType == ComponentType.Vegetable,
            MealConceptSlotType.Sauce => componentType is ComponentType.Sauce or ComponentType.Mixed,
            _ => true
        };
    }
}
