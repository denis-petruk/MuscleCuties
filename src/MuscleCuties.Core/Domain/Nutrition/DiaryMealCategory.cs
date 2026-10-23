using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>Stable owner-scoped identity for a diary section, including default and custom categories and historical entries after archive.</summary>
public sealed class DiaryMealCategory
{
    public const int MaxLabelLength = 40;
    public const int DefaultCategoryCount = 4;
    public const int MaxCustomCategoryCount = 8;
    public const int MaxTotalCategoryCount = DefaultCategoryCount + MaxCustomCategoryCount;

    private DiaryMealCategory(
        Guid id,
        Guid ownerId,
        string name,
        int displayOrder,
        bool isArchived,
        MealType? legacyMealType = null)
    {
        Id = id;
        OwnerId = ownerId;
        Name = name;
        DisplayOrder = displayOrder;
        IsArchived = isArchived;
        LegacyMealType = legacyMealType;
    }

    public Guid Id { get; }
    public Guid OwnerId { get; }
    public string Name { get; }
    public int DisplayOrder { get; }
    public bool IsArchived { get; }
    public MealType? LegacyMealType { get; }
    public bool IsDefaultSlot => LegacyMealType.HasValue;

    public static IReadOnlyList<DiaryMealCategory> CreateDefaultCategories(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("A valid owner ID is required.", nameof(ownerId));

        return
        [
            new DiaryMealCategory(CreateDeterministicDefaultId(ownerId, MealType.Breakfast), ownerId, "Breakfast", 0, false, MealType.Breakfast),
            new DiaryMealCategory(CreateDeterministicDefaultId(ownerId, MealType.Lunch), ownerId, "Lunch", 1, false, MealType.Lunch),
            new DiaryMealCategory(CreateDeterministicDefaultId(ownerId, MealType.Dinner), ownerId, "Dinner", 2, false, MealType.Dinner),
            new DiaryMealCategory(CreateDeterministicDefaultId(ownerId, MealType.Snack), ownerId, "Snack", 3, false, MealType.Snack)
        ];
    }

    public static DiaryMealCategory Create(
        Guid id,
        Guid ownerId,
        string name,
        int displayOrder,
        IReadOnlyList<DiaryMealCategory> existing,
        MealType? legacyMealType = null)
    {
        ValidateIdentityAndOrder(id, ownerId, displayOrder);
        ArgumentNullException.ThrowIfNull(existing);
        if (!legacyMealType.HasValue)
        {
            var customCount = existing.Count(c => c.OwnerId == ownerId && !c.LegacyMealType.HasValue && c.Id != id);
            if (customCount >= MaxCustomCategoryCount)
                throw new InvalidOperationException($"Cannot exceed {MaxCustomCategoryCount} custom meal categories.");
        }

        var normalizedName = NormalizeName(name);
        RequireUniqueName(ownerId, id, normalizedName, existing);
        return new DiaryMealCategory(id, ownerId, normalizedName, displayOrder, false, legacyMealType);
    }

    public DiaryMealCategory Rename(string name, IReadOnlyList<DiaryMealCategory> existing)
    {
        var normalizedName = NormalizeName(name);
        RequireUniqueName(OwnerId, Id, normalizedName, existing);
        return new DiaryMealCategory(Id, OwnerId, normalizedName, DisplayOrder, IsArchived, LegacyMealType);
    }

    public DiaryMealCategory Reorder(int displayOrder)
    {
        ValidateIdentityAndOrder(Id, OwnerId, displayOrder);
        return new DiaryMealCategory(Id, OwnerId, Name, displayOrder, IsArchived, LegacyMealType);
    }

    public DiaryMealCategory Archive() =>
        new(Id, OwnerId, Name, DisplayOrder, true, LegacyMealType);

    public DiaryMealCategory Unarchive(IReadOnlyList<DiaryMealCategory> existing)
    {
        RequireUniqueName(OwnerId, Id, Name, existing);
        return new DiaryMealCategory(Id, OwnerId, Name, DisplayOrder, false, LegacyMealType);
    }

    private static Guid CreateDeterministicDefaultId(Guid ownerId, MealType mealType)
    {
        var bytes = ownerId.ToByteArray();
        bytes[15] = (byte)(bytes[15] ^ ((int)mealType + 1));
        return new Guid(bytes);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A meal category name is required.", nameof(name));
        var normalized = name.Trim();
        if (normalized.Length > MaxLabelLength)
            throw new ArgumentException($"Meal category name cannot exceed {MaxLabelLength} characters.", nameof(name));
        return normalized;
    }

    private static void ValidateIdentityAndOrder(Guid id, Guid ownerId, int displayOrder)
    {
        if (id == Guid.Empty || ownerId == Guid.Empty)
            throw new ArgumentException("A category and owner ID are required.");
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
    }

    private static void RequireUniqueName(Guid ownerId, Guid currentId, string name,
        IReadOnlyList<DiaryMealCategory> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        foreach (var item in existing.Where(item => item.OwnerId == ownerId && item.Id != currentId &&
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            if (item.IsArchived)
                throw new InvalidOperationException("Archived category name reuse requires unarchiving or renaming the archived category.");
            throw new ArgumentException("A meal category with this name already exists.", nameof(name));
        }
    }
}
