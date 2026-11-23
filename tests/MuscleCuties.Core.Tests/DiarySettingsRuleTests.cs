using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.BatchAndCopy;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class DiarySettingsRuleTests
{
    private static readonly Guid Owner = Guid.Parse("31000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherOwner = Guid.Parse("31000000-0000-0000-0000-000000000002");
    private static readonly Guid CategoryId = Guid.Parse("31000000-0000-0000-0000-000000000003");
    private static readonly Guid OtherCategoryId = Guid.Parse("31000000-0000-0000-0000-000000000004");
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateOnly Tuesday = new(2026, 10, 20);
    private const int InitialDisplayOrder = 2;
    private const int NewDisplayOrder = 3;

    [Fact]
    public void CategoryRenameArchiveAndReorderPreserveOwnerAndIdentity()
    {
        var original = DiaryMealCategory.Create(CategoryId, Owner, "After gym", InitialDisplayOrder, []);
        var renamed = original.Rename(" Evening meal ", [original]);
        var archived = renamed.Reorder(NewDisplayOrder).Archive();

        Assert.Equal(CategoryId, archived.Id);
        Assert.Equal(Owner, archived.OwnerId);
        Assert.Equal("Evening meal", archived.Name);
        Assert.Equal(NewDisplayOrder, archived.DisplayOrder);
        Assert.True(archived.IsArchived);
    }

    [Fact]
    public void CategoryRejectsDuplicateAndInvalidNamesWithinOwner()
    {
        var existing = DiaryMealCategory.Create(CategoryId, Owner, "Snack", InitialDisplayOrder, []);

        Assert.Throws<ArgumentException>(() => DiaryMealCategory.Create(OtherCategoryId, Owner,
            " snack ", NewDisplayOrder, [existing]));
        Assert.Throws<ArgumentException>(() => existing.Rename(" ", [existing]));
        Assert.Throws<ArgumentException>(() => DiaryMealCategory.Create(Guid.Empty, Owner,
            "Meal", NewDisplayOrder, []));
        Assert.Throws<InvalidOperationException>(() => DiaryMealCategory.Create(OtherCategoryId, Owner,
            "Snack", NewDisplayOrder, [existing.Archive()]));

        var otherOwnersSameName = DiaryMealCategory.Create(OtherCategoryId, OtherOwner,
            "snack", NewDisplayOrder, [existing]);
        Assert.Equal(OtherOwner, otherOwnersSameName.OwnerId);
    }
}
