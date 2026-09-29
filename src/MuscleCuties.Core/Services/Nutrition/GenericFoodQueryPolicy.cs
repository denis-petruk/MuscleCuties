using System.Text;

namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>
/// Fail-closed boundary for food names sent to an external catalog. Local food
/// search still accepts the original text; only recognized generic terms leave
/// the device.
/// </summary>
public static class GenericFoodQueryPolicy
{
    private const int MaximumLength = 80;
    private const int MaximumWords = 6;

    private static readonly HashSet<string> Foods = new(StringComparer.Ordinal)
    {
        "almond", "almonds", "anchovy", "apple", "apples", "apricot", "arugula", "asparagus",
        "avocado", "bacon", "bagel", "banana", "bananas", "barley", "basil", "bean", "beans",
        "beef", "beet", "beets", "blackberry", "blackberries", "blueberry", "blueberries",
        "bread", "broccoli", "broth", "buckwheat", "bulgur", "burger", "butter", "cabbage",
        "cake", "cantaloupe", "carrot", "carrots", "cashew", "cashews", "cauliflower", "celery",
        "cereal", "cheddar", "cheese", "cherry", "cherries", "chicken", "chickpea", "chickpeas",
        "chocolate", "cinnamon", "cilantro", "cod", "coconut", "coffee", "corn", "cornmeal",
        "couscous", "crab", "cream", "cucumber", "cumin", "date", "dates", "dill",
        "edamame", "egg", "eggs", "eggplant", "farro", "feta", "fig", "figs", "fish", "flax",
        "flour", "garlic", "ginger", "granola", "grape", "grapes", "ham", "hazelnut",
        "hazelnuts", "honey", "hummus", "kale", "kefir", "kiwi", "lamb", "lemon", "lentil",
        "lentils", "lettuce", "lime", "lobster", "mackerel", "mango", "mayonnaise",
        "mayo", "meat", "meatball", "milk", "millet", "mozzarella", "mushroom", "mushrooms",
        "mustard", "noodles", "oatmeal", "oats", "oil", "olive", "olives", "onion", "orange",
        "oranges", "oregano", "pancake", "pancakes", "paprika", "parmesan", "pasta", "pea",
        "peach", "peaches", "peanut", "peanuts", "pear", "pears", "peas", "pecan", "pecans",
        "pepper", "peppers", "pie", "pineapple", "pistachio", "pistachios", "pita", "plum",
        "plums", "polenta", "pomegranate", "pork", "potato", "potatoes", "prawn", "pumpkin",
        "quinoa", "radish", "raspberry", "raspberries", "rice", "ricotta", "rosemary", "rye",
        "salad", "salmon", "salsa", "salt", "sandwich", "sardine", "sausage", "seitan",
        "sesame", "shrimp", "smoothie", "soup", "soy", "spinach", "squash", "stock",
        "strawberry", "strawberries", "sugar", "sunflower", "tahini", "tea",
        "tempeh", "thyme", "tilapia", "tofu", "tomato", "tomatoes", "tortilla", "trout",
        "tuna", "turkey", "turmeric", "turnip", "vinegar", "waffle", "waffles", "walnut",
        "walnuts", "water", "watermelon", "wheat", "yogurt", "yoghurt", "zucchini"
    };

    private static readonly HashSet<string> Descriptors = new(StringComparer.Ordinal)
    {
        "baked", "boiled", "boneless", "breast", "brown", "canned", "chopped", "cooked", "cottage", "diced",
        "dried", "extra", "fat", "fresh", "fried", "frozen", "greek", "grilled", "ground", "green",
        "lean", "low", "maple", "nonfat", "organic", "plain", "raw", "red", "roasted", "shredded",
        "skinless", "skim", "sliced", "smoked", "steamed", "sweet", "thigh", "unsweetened", "virgin", "white",
        "whole", "wholegrain", "wild"
    };

    public static string NormalizeOrEmpty(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaximumLength)
            return string.Empty;

        var normalized = new StringBuilder(query.Length);
        foreach (var character in query.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z')
                normalized.Append(character);
            else if (character is ' ' or '-' or ',')
                normalized.Append(' ');
            else
                return string.Empty;
        }

        var words = normalized.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > MaximumWords || !words.Any(Foods.Contains) ||
            words.Any(word => !Foods.Contains(word) && !Descriptors.Contains(word)))
            return string.Empty;

        return string.Join(' ', words);
    }
}
