namespace MuscleCuties.Core.Models.Enums.Nutrition;

/// <summary>The role a meal component plays, which the planner fills.</summary>
public enum ComponentRole
{
    CarbBase,
    ProteinBase,
    SideSalad,
    InDishVegetable,
    SauceOnSide,
    SaucePairedProtein,
    InDishSauce
}

/// <summary>What kind of food a component is, which decides the foods it may hold.</summary>
public enum ComponentType
{
    Protein,
    Carb,
    Vegetable,
    Sauce,
    Mixed
}

/// <summary>The slots a meal concept fills, so a generated day is complete.</summary>
public enum MealConceptSlotType
{
    CarbBase,
    ProteinBase,
    VitaminBase,
    Sauce
}

/// <summary>The style a meal is planned in, which filters the recipes it may use.</summary>
public enum MealStyle
{
    Plated,
    Bowl,
    Complex,
    Handheld,
    Wrapped
}
