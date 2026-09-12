using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition.Planning;

internal static class SavouryMealCatalog
{
    private static readonly IReadOnlySet<DietaryTag> NoDietaryRestrictions =
        new HashSet<DietaryTag>();

    public static IReadOnlyList<MealConcept> GetConcepts(MealType mealType)
    {
        return mealType switch
        {
            MealType.Breakfast => BreakfastConcepts,
            MealType.Lunch => LunchConcepts,
            MealType.Dinner => DinnerConcepts,
            MealType.Snack => SnackConcepts,
            _ => []
        };
    }

    private static readonly IReadOnlyList<MealConcept> BreakfastConcepts =
    [
        Concept(
            "Shakshuka",
            "Eggs poached in spiced tomato sauce served with bread.",
            [MealType.Breakfast],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["bread", "whole wheat bread"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["eggs"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["bell pepper"], 0.4f, true),
                    Entry(["onion"], 0.3f, false),
                    Entry(["tomato"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["olive oil"], 0.4f, true),
                    Entry(["crushed tomato"], 0.6f, false)
                ], true)
            ],
            [
                Spice("Cumin + paprika + cayenne", "Classic North African warmth with gentle heat."),
                Spice("Za'atar + sumac", "Herby and tangy Middle Eastern blend."),
                Spice("Ras el hanout", "Aromatic Moroccan spice blend with cinnamon and coriander.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Avocado Toast",
            "Toasted bread with avocado, egg, and fresh tomato.",
            [MealType.Breakfast],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["bread", "whole wheat bread"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["eggs"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [Entry(["tomato"], 1f, true)], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["avocado"], 0.7f, true),
                    Entry(["sriracha"], 0.3f, false)
                ], true)
            ],
            [
                Spice("Everything bagel seasoning", "Sesame, poppy, garlic, onion, and sea salt."),
                Spice("Chili flakes + lime zest", "Bright and spicy citrus kick."),
                Spice("Furikake", "Japanese seaweed and sesame topping.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Breakfast Taco",
            "Whole wheat tortilla with scrambled protein, fresh pico, and a creamy sauce.",
            [MealType.Breakfast],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["tortilla", "flour tortilla"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["eggs", "ground turkey"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["onion"], 0.5f, true),
                    Entry(["tomato"], 0.5f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["sour cream"], 0.6f, true),
                    Entry(["hot sauce"], 0.4f, false)
                ], true)
            ],
            [
                Spice("Cumin + chili powder + garlic powder", "Classic taco seasoning."),
                Spice("Chipotle + lime zest", "Smoky with a bright citrus note."),
                Spice("Adobo seasoning", "Garlic, oregano, black pepper, and turmeric.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Breakfast Wrap",
            "Whole wheat tortilla wrapped with sliced meat, eggs, greens, and a spicy drizzle.",
            [MealType.Breakfast],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["tortilla", "flour tortilla"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["turkey ham", "eggs"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["lettuce"], 0.5f, true),
                    Entry(["tomato"], 0.5f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["sour cream"], 0.5f, true),
                    Entry(["sriracha"], 0.5f, false)
                ], true)
            ],
            [
                Spice("Smoked paprika + garlic powder", "Deep smoky warmth."),
                Spice("Italian herb blend", "Oregano, basil, thyme, and rosemary."),
                Spice("Cumin + coriander", "Earthy and aromatic.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Breakfast Burger",
            "Whole wheat bun with a lean patty, greens, and a tangy yogurt sauce.",
            [MealType.Breakfast],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["hamburger bun", "bun"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["ground turkey", "ground beef", "ground chicken"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["lettuce"], 0.4f, true),
                    Entry(["tomato"], 0.3f, false),
                    Entry(["onion"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["greek yogurt"], 0.5f, true),
                    Entry(["mustard", "ketchup"], 0.5f, false)
                ], true)
            ],
            [
                Spice("Smoked paprika + garlic powder", "Classic burger spice."),
                Spice("Italian herb blend", "Oregano, basil, and garlic."),
                Spice("Cajun seasoning", "Paprika, cayenne, garlic, onion, and thyme.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Egg and Potato Plate",
            "Baked potato with eggs, greens, and a drizzle of olive oil.",
            [MealType.Breakfast],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["potato", "white potato"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["eggs"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["spinach"], 0.6f, true),
                    Entry(["bell pepper"], 0.4f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [Entry(["olive oil"], 1f, true)], true)
            ],
            [
                Spice("Turmeric + black pepper", "Anti-inflammatory golden combination."),
                Spice("Rosemary + garlic", "Classic herb roast pairing."),
                Spice("Paprika + onion powder", "Mild sweetness with depth.")
            ],
            NoDietaryRestrictions)
    ];

    private static readonly IReadOnlyList<MealConcept> LunchConcepts =
    [
        Concept(
            "Loaded Grain Bowl",
            "Quinoa or rice base with protein, fresh vegetables, and a healthy fat.",
            [MealType.Lunch],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["quinoa", "brown rice"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["chicken", "tuna", "tofu"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["bell pepper"], 0.4f, true),
                    Entry(["spinach"], 0.3f, false),
                    Entry(["tomato"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["olive oil"], 0.5f, true),
                    Entry(["avocado"], 0.5f, false)
                ], true)
            ],
            [
                Spice("Italian herbs + garlic", "Oregano, basil, thyme with garlic."),
                Spice("Lemon pepper + dill", "Light and refreshing."),
                Spice("Turmeric + cumin + coriander", "Warm golden spice blend.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Protein Wrap",
            "Whole wheat tortilla with lean protein, fresh greens, and a tangy dressing.",
            [MealType.Lunch],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["tortilla", "flour tortilla"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["chicken", "turkey ham"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["lettuce"], 0.4f, true),
                    Entry(["tomato"], 0.3f, false),
                    Entry(["onion"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["greek yogurt"], 0.6f, true),
                    Entry(["mustard"], 0.4f, false)
                ], true)
            ],
            [
                Spice("Garlic powder + onion powder + paprika", "All-purpose savory blend."),
                Spice("Lemon herb blend", "Lemon zest, dill, parsley, and garlic."),
                Spice("Cumin + smoked paprika", "Earthy with a smoky edge.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Bean Power Plate",
            "Beans with protein, leafy greens, and a tomato-based sauce.",
            [MealType.Lunch],
            [
                Slot(MealConceptSlotType.CarbBase, [
                    Entry(["chickpeas", "black beans", "pinto beans", "cannellini beans"], 0.7f, true),
                    Entry(["brown rice", "quinoa"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["chicken", "tofu"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["kale"], 0.6f, true),
                    Entry(["bell pepper"], 0.4f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["olive oil"], 0.4f, true),
                    Entry(["crushed tomato"], 0.6f, false)
                ], true)
            ],
            [
                Spice("Cumin + oregano + chili powder", "Tex-Mex warmth."),
                Spice("Smoked paprika + garlic", "Rich and smoky."),
                Spice("Herbs de Provence", "Lavender, thyme, rosemary, and savory.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Taco Bowl",
            "Rice or beans topped with seasoned protein, fresh salsa, and a creamy finish.",
            [MealType.Lunch],
            [
                Slot(MealConceptSlotType.CarbBase, [
                    Entry(["brown rice"], 0.6f, true),
                    Entry(["pinto beans", "black beans"], 0.4f, false)
                ], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["ground turkey", "chicken", "ground beef"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["lettuce"], 0.4f, true),
                    Entry(["tomato"], 0.3f, false),
                    Entry(["onion"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["sour cream"], 0.5f, true),
                    Entry(["hot sauce"], 0.5f, false)
                ], true)
            ],
            [
                Spice("Taco seasoning (cumin + chili + garlic)", "Classic taco bowl spice."),
                Spice("Chipotle + lime", "Smoky and tangy."),
                Spice("Adobo + oregano", "Puerto Rican-style seasoning.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Loaded Potato",
            "Baked potato with protein, greens, and a creamy topping.",
            [MealType.Lunch],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["potato", "white potato"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["chicken", "ground beef", "ground turkey"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["spinach"], 0.6f, true),
                    Entry(["onion"], 0.4f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["greek yogurt", "sour cream"], 1f, true)
                ], true)
            ],
            [
                Spice("Garlic + chives + black pepper", "Classic loaded potato spice."),
                Spice("Smoked paprika + cumin", "Smoky and earthy."),
                Spice("Italian seasoning + garlic", "Herb-forward and savory.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Quinoa Salad Plate",
            "Quinoa with protein, colorful vegetables, and olive oil dressing.",
            [MealType.Lunch],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["quinoa"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["tuna", "chickpeas"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["lettuce"], 0.4f, true),
                    Entry(["tomato"], 0.3f, false),
                    Entry(["bell pepper"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["olive oil"], 0.6f, true),
                    Entry(["avocado"], 0.4f, false)
                ], true)
            ],
            [
                Spice("Lemon + oregano + garlic", "Bright Mediterranean dressing."),
                Spice("Sumac + parsley + sesame", "Middle Eastern salad spice."),
                Spice("Balsamic + basil + black pepper", "Italian-inspired dressing.")
            ],
            NoDietaryRestrictions)
    ];

    private static readonly IReadOnlyList<MealConcept> DinnerConcepts =
    [
        Concept(
            "Protein Sweet Potato Plate",
            "Baked sweet potato with protein and leafy greens drizzled with olive oil.",
            [MealType.Dinner],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["sweet potato"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["salmon", "chicken"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["kale"], 0.5f, true),
                    Entry(["spinach"], 0.5f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [Entry(["olive oil"], 1f, true)], true)
            ],
            [
                Spice("Garlic + rosemary + thyme", "Classic roast herb blend."),
                Spice("Cumin + cinnamon + paprika", "Warm sweet potato spice."),
                Spice("Lemon pepper + dill", "Light and herby.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Bean Stew Bowl",
            "Hearty bean stew with protein and vegetables in a tomato base.",
            [MealType.Dinner],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["cannellini beans", "black beans", "pinto beans"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["chicken", "tofu"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["onion"], 0.3f, true),
                    Entry(["tomato"], 0.4f, false),
                    Entry(["bell pepper"], 0.3f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["crushed tomato"], 0.7f, true),
                    Entry(["olive oil"], 0.3f, false)
                ], true)
            ],
            [
                Spice("Cumin + paprika + oregano", "Warming stew blend."),
                Spice("Bay leaf + thyme + black pepper", "Classic stew aromatics."),
                Spice("Chili powder + garlic + onion powder", "Bold and savory.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Rice Protein Bowl",
            "Brown rice bowl with protein and greens finished with olive oil.",
            [MealType.Dinner],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["brown rice"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["salmon", "chicken", "ground turkey"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["spinach"], 0.6f, true),
                    Entry(["bell pepper"], 0.4f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [Entry(["olive oil"], 1f, true)], true)
            ],
            [
                Spice("Garlic + ginger + sesame", "Asian-inspired bowl spice."),
                Spice("Turmeric + cumin + coriander", "Golden spice blend."),
                Spice("Italian herbs + lemon zest", "Mediterranean finish.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Stuffed Potato",
            "Baked potato filled with seasoned protein, onion, and a creamy topping.",
            [MealType.Dinner],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["potato", "white potato"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["ground beef", "ground turkey"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["onion"], 0.5f, true),
                    Entry(["tomato"], 0.5f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["sour cream", "greek yogurt"], 1f, true)
                ], true)
            ],
            [
                Spice("Garlic + chives + paprika", "Classic stuffed potato topping."),
                Spice("Cumin + chili powder + oregano", "Southwest filling spice."),
                Spice("Smoked paprika + black pepper + onion powder", "Rich and smoky.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Lentil Power Bowl",
            "Lentils with protein and greens in a tomato-olive oil base.",
            [MealType.Dinner],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["lentils"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["eggs", "tofu"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["kale"], 0.5f, true),
                    Entry(["tomato"], 0.5f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [
                    Entry(["olive oil"], 0.4f, true),
                    Entry(["crushed tomato"], 0.6f, false)
                ], true)
            ],
            [
                Spice("Cumin + turmeric + garam masala", "Indian dal-style warmth."),
                Spice("Smoked paprika + garlic + lemon", "Mediterranean approach."),
                Spice("Curry powder + ginger", "Quick curry bowl spice.")
            ],
            NoDietaryRestrictions)
    ];

    private static readonly IReadOnlyList<MealConcept> SnackConcepts =
    [
        Concept(
            "Mini Sandwich",
            "A small open-face or half sandwich with lean protein and greens.",
            [MealType.Snack],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["bread", "whole wheat bread"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["turkey ham", "tuna"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["lettuce"], 0.5f, true),
                    Entry(["tomato"], 0.5f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [Entry(["mustard"], 1f, false)], false)
            ],
            [
                Spice("Black pepper + dried dill", "Simple herb finish."),
                Spice("Everything seasoning", "Sesame, poppy, garlic, onion, salt."),
                Spice("Paprika + garlic powder", "Mild warmth.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Protein Snack Plate",
            "Hummus or yogurt dip with crunchy vegetables and seeds.",
            [MealType.Snack],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["hummus"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["pumpkin seeds"], 1f, false)], false),
                Slot(MealConceptSlotType.VitaminBase, [
                    Entry(["carrot"], 0.5f, true),
                    Entry(["bell pepper"], 0.5f, false)
                ], true),
                Slot(MealConceptSlotType.Sauce, [Entry(["olive oil"], 1f, false)], false)
            ],
            [
                Spice("Za'atar + lemon", "Bright Middle Eastern sprinkle."),
                Spice("Paprika + cumin", "Warm hummus topping."),
                Spice("Sesame + sea salt", "Simple and crunchy.")
            ],
            NoDietaryRestrictions),

        Concept(
            "Yogurt Bowl",
            "Greek yogurt with berries and crunchy seeds.",
            [MealType.Snack],
            [
                Slot(MealConceptSlotType.CarbBase, [Entry(["blueberries", "strawberries", "raspberries"], 1f, true)], true),
                Slot(MealConceptSlotType.ProteinBase, [Entry(["greek yogurt"], 1f, true)], true),
                Slot(MealConceptSlotType.VitaminBase, [Entry(["blueberries", "strawberries"], 1f, false)], false),
                Slot(MealConceptSlotType.Sauce, [Entry(["pumpkin seeds", "chia seeds"], 1f, false)], false)
            ],
            [
                Spice("Cinnamon + vanilla", "Warm and sweet."),
                Spice("Cardamom + honey drizzle", "Aromatic and floral."),
                Spice("Nutmeg + ginger", "Spiced warmth.")
            ],
            NoDietaryRestrictions)
    ];

    private static MealConcept Concept(
        string name,
        string description,
        IReadOnlyList<MealType> mealTypes,
        IReadOnlyList<MealConceptSlot> slots,
        IReadOnlyList<SpiceBlendOption> spiceBlends,
        IReadOnlySet<DietaryTag> incompatibleDietaryTags)
    {
        return new MealConcept(name, description, mealTypes, slots, spiceBlends, incompatibleDietaryTags);
    }

    private static MealConceptSlot Slot(
        MealConceptSlotType slotType,
        IReadOnlyList<SlotIngredientEntry> ingredientEntries,
        bool required)
    {
        return new MealConceptSlot(slotType, ingredientEntries, required);
    }

    private static SlotIngredientEntry Entry(
        IReadOnlyList<string> preferredFoodTerms,
        float portionShare,
        bool required)
    {
        return new SlotIngredientEntry(preferredFoodTerms, portionShare, required);
    }

    private static SpiceBlendOption Spice(string name, string description)
    {
        return new SpiceBlendOption(name, description);
    }
}
