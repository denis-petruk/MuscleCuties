using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

internal static class SweetBreakfastCatalog
{
    public static IReadOnlyList<MealConcept> GetConcepts()
    {
        return Concepts;
    }

    private static readonly IReadOnlyList<MealConcept> Concepts =
    [
        new MealConcept(
            "Berry Oat Bowl",
            "Rolled oats with berries and a serving of yogurt for protein.",
            [MealType.Breakfast],
            [
                new MealConceptSlot(MealConceptSlotType.CarbBase,
                    [new SlotIngredientEntry(["oats", "rolled oats", "gluten-free rolled oats"], 1f, true)], true),
                new MealConceptSlot(MealConceptSlotType.VitaminBase, [
                    new SlotIngredientEntry(["blueberries"], 0.5f, true),
                    new SlotIngredientEntry(["strawberries", "raspberries"], 0.5f, false)
                ], true),
                new MealConceptSlot(MealConceptSlotType.Sauce,
                    [new SlotIngredientEntry(["honey", "maple syrup"], 1f, true)], true),
                new MealConceptSlot(MealConceptSlotType.ProteinBase,
                    [new SlotIngredientEntry(["greek yogurt", "soy yogurt", "silken tofu", "pea protein"], 1f, true)], true)
            ],
            [
                new SpiceBlendOption("Cinnamon + vanilla", "Warm, classic oatmeal spice."),
                new SpiceBlendOption("Cinnamon + nutmeg + ginger", "Spiced autumn blend."),
                new SpiceBlendOption("Cardamom + turmeric", "Golden anti-inflammatory warmth.")
            ],
            new HashSet<DietaryTag>()),

        new MealConcept(
            "Yogurt Parfait",
            "Protein-rich yogurt layered with berries, oats or chia seeds, and a touch of sweetness.",
            [MealType.Breakfast],
            [
                new MealConceptSlot(MealConceptSlotType.ProteinBase,
                    [new SlotIngredientEntry(["greek yogurt", "soy yogurt", "silken tofu"], 1f, true)], true),
                new MealConceptSlot(MealConceptSlotType.VitaminBase, [
                    new SlotIngredientEntry(["blueberries"], 0.5f, true),
                    new SlotIngredientEntry(["strawberries", "raspberries"], 0.5f, false)
                ], true),
                new MealConceptSlot(MealConceptSlotType.CarbBase,
                    [new SlotIngredientEntry(["oats", "rolled oats", "chia seeds"], 1f, true)], true),
                new MealConceptSlot(MealConceptSlotType.Sauce,
                    [new SlotIngredientEntry(["honey", "maple syrup"], 1f, true)], true)
            ],
            [
                new SpiceBlendOption("Cinnamon + vanilla", "Classic parfait flavor."),
                new SpiceBlendOption("Cardamom + rose water", "Floral and aromatic."),
                new SpiceBlendOption("Ginger + nutmeg", "Gently warming spice.")
            ],
            new HashSet<DietaryTag>()),

        new MealConcept(
            "Banana Protein Oats",
            "Oats with banana and a serving of yogurt or plant protein.",
            [MealType.Breakfast],
            [
                new MealConceptSlot(MealConceptSlotType.CarbBase,
                    [new SlotIngredientEntry(["oats", "rolled oats", "gluten-free rolled oats"], 1f, true)], true),
                new MealConceptSlot(MealConceptSlotType.VitaminBase,
                    [new SlotIngredientEntry(["banana"], 1f, true)], true),
                new MealConceptSlot(MealConceptSlotType.ProteinBase,
                    [new SlotIngredientEntry(["greek yogurt", "soy yogurt", "silken tofu", "pea protein"], 1f, true)], true),
                new MealConceptSlot(MealConceptSlotType.Sauce, [
                    new SlotIngredientEntry(["dark chocolate"], 0.5f, false),
                    new SlotIngredientEntry(["honey"], 0.5f, false)
                ], false)
            ],
            [
                new SpiceBlendOption("Cinnamon + vanilla", "Warm comfort combination."),
                new SpiceBlendOption("Cinnamon + nutmeg + ginger", "Spiced dessert-style oats."),
                new SpiceBlendOption("Cardamom + turmeric", "Golden warmth with depth.")
            ],
            new HashSet<DietaryTag>())
    ];
}
