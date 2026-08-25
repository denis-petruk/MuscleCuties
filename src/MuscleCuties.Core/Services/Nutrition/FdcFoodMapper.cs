using System.Text.Json;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public static class FdcFoodMapper
{
    private const int Calories = 1008;
    private const int CaloriesAtwaterGeneral = 2047;
    private const int CaloriesAtwaterSpecific = 2048;
    private const int Protein = 1003;
    private const int Carbs = 1005;
    private const int Fats = 1004;
    private const int Fiber = 1079;
    private const int Iron = 1089;
    private const int VitaminB12 = 1178;
    private const int VitaminC = 1162;
    private const int VitaminD = 1114;
    private const int VitaminA = 1104;
    private const int VitaminB6 = 1175;
    private const int Folate = 1177;
    private const int Calcium = 1087;
    private const int Magnesium = 1090;
    private const int Zinc = 1095;
    private const int Potassium = 1092;

    private const string CaloriesNumber = "208";
    private const string ProteinNumber = "203";
    private const string CarbsNumber = "205";
    private const string FatsNumber = "204";
    private const string FiberNumber = "291";
    private const string IronNumber = "303";
    private const string VitaminB12Number = "418";
    private const string VitaminCNumber = "401";
    private const string VitaminDNumber = "328";
    private const string VitaminANumber = "318";
    private const string VitaminB6Number = "415";
    private const string FolateNumber = "417";
    private const string CalciumNumber = "301";
    private const string MagnesiumNumber = "304";
    private const string ZincNumber = "309";
    private const string PotassiumNumber = "306";

    public static FoodItem ApplyToFoodItem(FdcFoodDetail detail, FoodItem? existing = null, DateTime? syncedAt = null)
    {
        var now = syncedAt ?? DateTime.UtcNow;
        var item = existing ?? new FoodItem { CreatedAt = now };

        item.FdcId = detail.FdcId;
        item.Name = string.IsNullOrWhiteSpace(detail.Description)
            ? $"FDC food {detail.FdcId}"
            : FoodSearchResultFilter.FormatFoodName(detail.Description);
        item.Calories = GetCalories(detail);
        item.Protein = GetNutrient(detail, Protein, ProteinNumber);
        item.Carbs = GetNutrient(detail, Carbs, CarbsNumber);
        item.Fats = GetNutrient(detail, Fats, FatsNumber);
        item.Fiber = GetNutrient(detail, Fiber, FiberNumber);
        item.Iron = GetNutrient(detail, Iron, IronNumber);
        item.VitaminB12 = GetNutrient(detail, VitaminB12, VitaminB12Number);
        item.VitaminC = GetNutrient(detail, VitaminC, VitaminCNumber);
        item.VitaminD = GetNutrient(detail, VitaminD, VitaminDNumber);
        item.VitaminA = GetNutrient(detail, VitaminA, VitaminANumber);
        item.VitaminB6 = GetNutrient(detail, VitaminB6, VitaminB6Number);
        item.Folate = GetNutrient(detail, Folate, FolateNumber);
        item.Calcium = GetNutrient(detail, Calcium, CalciumNumber);
        item.Magnesium = GetNutrient(detail, Magnesium, MagnesiumNumber);
        item.Zinc = GetNutrient(detail, Zinc, ZincNumber);
        item.Potassium = GetNutrient(detail, Potassium, PotassiumNumber);
        item.DataType = Clean(detail.DataType);
        item.BrandOwner = Clean(detail.BrandOwner);
        item.BrandName = Clean(detail.BrandName);
        item.GtinUpc = Clean(detail.GtinUpc);
        item.Ingredients = Clean(detail.Ingredients);
        item.ServingSize = detail.ServingSize;
        item.ServingSizeUnit = Clean(detail.ServingSizeUnit);
        item.ServingOptionsJson = FoodServingOptions.CreateOptionsJson(detail);
        item.IsCustom = false;
        item.LastSyncedAt = now;
        item.UpdatedAt = now;

        return item;
    }

    public static bool HasNutrientChanges(FoodItem existing, FdcFoodDetail detail) =>
        HasChanged(existing.Calories, GetCalories(detail)) ||
        HasChanged(existing.Protein, GetNutrient(detail, Protein, ProteinNumber)) ||
        HasChanged(existing.Carbs, GetNutrient(detail, Carbs, CarbsNumber)) ||
        HasChanged(existing.Fats, GetNutrient(detail, Fats, FatsNumber)) ||
        HasChanged(existing.Fiber, GetNutrient(detail, Fiber, FiberNumber)) ||
        HasChanged(existing.Iron, GetNutrient(detail, Iron, IronNumber)) ||
        HasChanged(existing.VitaminB12, GetNutrient(detail, VitaminB12, VitaminB12Number)) ||
        HasChanged(existing.VitaminC, GetNutrient(detail, VitaminC, VitaminCNumber)) ||
        HasChanged(existing.VitaminD, GetNutrient(detail, VitaminD, VitaminDNumber)) ||
        HasChanged(existing.VitaminA, GetNutrient(detail, VitaminA, VitaminANumber)) ||
        HasChanged(existing.VitaminB6, GetNutrient(detail, VitaminB6, VitaminB6Number)) ||
        HasChanged(existing.Folate, GetNutrient(detail, Folate, FolateNumber)) ||
        HasChanged(existing.Calcium, GetNutrient(detail, Calcium, CalciumNumber)) ||
        HasChanged(existing.Magnesium, GetNutrient(detail, Magnesium, MagnesiumNumber)) ||
        HasChanged(existing.Zinc, GetNutrient(detail, Zinc, ZincNumber)) ||
        HasChanged(existing.Potassium, GetNutrient(detail, Potassium, PotassiumNumber)) ||
        !string.Equals(
            existing.ServingOptionsJson,
            FoodServingOptions.CreateOptionsJson(detail),
            StringComparison.Ordinal);

    public static string CreateNutrientSnapshot(FoodItem item) =>
        JsonSerializer.Serialize(new
        {
            item.Calories,
            item.Protein,
            item.Carbs,
            item.Fats,
            item.Fiber,
            item.Iron,
            item.VitaminB12,
            item.VitaminC,
            item.VitaminD,
            item.VitaminA,
            item.VitaminB6,
            item.Folate,
            item.Calcium,
            item.Magnesium,
            item.Zinc,
            item.Potassium,
            item.DataType,
            item.BrandOwner,
            item.BrandName,
            item.GtinUpc,
            item.Ingredients,
            item.ServingSize,
            item.ServingSizeUnit,
            item.ServingOptionsJson
        });

    private static float GetNutrient(FdcFoodDetail detail, int nutrientId, string nutrientNumber)
    {
        var nutrient = detail.FoodNutrients
            .FirstOrDefault(n => IsNutrient(n, nutrientId, nutrientNumber));

        return nutrient is null ? 0f : GetAmount(nutrient);
    }

    private static float GetCalories(FdcFoodDetail detail)
    {
        var calories = GetNutrient(detail, Calories, CaloriesNumber);
        if (calories > 0f)
            return calories;

        var atwater = detail.FoodNutrients.FirstOrDefault(IsAtwaterCalorieNutrient);
        if (atwater is not null)
            return GetAmount(atwater);

        var namedEnergy = detail.FoodNutrients.FirstOrDefault(IsNamedCalorieNutrient);
        return namedEnergy is null ? 0f : GetAmount(namedEnergy);
    }

    private static bool IsNutrient(FdcFoodDetailNutrient nutrient, int nutrientId, string nutrientNumber)
    {
        var id = nutrient.Nutrient?.Id > 0 ? nutrient.Nutrient.Id : nutrient.NutrientId;
        if (id == nutrientId)
            return true;

        return IsNutrientNumber(nutrient.Nutrient?.Number, nutrientNumber) ||
               IsNutrientNumber(nutrient.Number, nutrientNumber) ||
               IsNutrientNumber(nutrient.NutrientNumber, nutrientNumber);
    }

    private static bool IsNutrientNumber(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return false;

        return string.Equals(
            actual.Trim().TrimStart('0'),
            expected.TrimStart('0'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAtwaterCalorieNutrient(FdcFoodDetailNutrient nutrient)
    {
        var id = nutrient.Nutrient?.Id > 0 ? nutrient.Nutrient.Id : nutrient.NutrientId;
        return id is CaloriesAtwaterGeneral or CaloriesAtwaterSpecific;
    }

    private static bool IsNamedCalorieNutrient(FdcFoodDetailNutrient nutrient)
    {
        var unitName = nutrient.UnitName ?? nutrient.Nutrient?.UnitName;
        if (!string.Equals(unitName, "KCAL", StringComparison.OrdinalIgnoreCase))
            return false;

        var name = nutrient.NutrientName ?? nutrient.Name ?? nutrient.Nutrient?.Name;
        return name is not null &&
               name.Contains("Energy", StringComparison.OrdinalIgnoreCase);
    }

    private static float GetAmount(FdcFoodDetailNutrient nutrient) =>
        nutrient.Amount ?? nutrient.Value ?? 0f;

    private static bool HasChanged(float current, float next) =>
        Math.Abs(current - next) > 0.001f;

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
