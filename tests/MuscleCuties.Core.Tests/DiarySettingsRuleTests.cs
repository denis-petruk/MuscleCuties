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

    [Fact]
    public void EnabledWeekdayOverrideReplacesCalculatedTargetAndPerMealNeedNotSum()
    {
        var calculated = DiaryNutritionTarget.Create(1800m, 100m, 200m, 60m);
        var overrideTarget = DiaryNutritionTarget.Create(2000m, 120m, 210m, 70m);
        var perMeal = DiaryPerMealTarget.Create(Owner, CategoryId,
            DiaryNutritionTarget.Create(350m, 20m, 30m, 10m));
        var weekdayOverride = DiaryWeekdayOverride.Create(Owner, Monday.DayOfWeek, true, overrideTarget);

        var effective = DiaryTargetPolicy.Resolve(Monday, Owner, calculated, weekdayOverride);

        Assert.Same(overrideTarget, effective.Target);
        Assert.Equal(DiaryTargetSource.UserWeekdayOverride, effective.Source);
        Assert.Equal(350m, perMeal.Target.Calories);
    }

    [Fact]
    public void DisabledOverrideFallsBackAndMissingCalculatedTargetRemainsUnavailable()
    {
        var calculated = DiaryNutritionTarget.Create(1800m, 100m, 200m, 60m);
        var disabled = DiaryWeekdayOverride.Create(Owner, Tuesday.DayOfWeek, false,
            DiaryNutritionTarget.Create(2000m, 120m, 210m, 70m));

        var fallback = DiaryTargetPolicy.Resolve(Tuesday, Owner, calculated, disabled);
        var missing = DiaryTargetPolicy.Resolve(Tuesday, Owner, null, disabled);

        Assert.Same(calculated, fallback.Target);
        Assert.Equal(DiaryTargetSource.Calculated, fallback.Source);
        Assert.Null(missing.Target);
        Assert.Equal(DiaryTargetSource.Unavailable, missing.Source);
    }

    [Fact]
    public void WeekdayAndOwnerMismatchRejectStaleOrCrossAccountOverride()
    {
        var mondayOverride = DiaryWeekdayOverride.Create(Owner, Monday.DayOfWeek, true,
            DiaryNutritionTarget.Create(2000m, 120m, 210m, 70m));
        Assert.Throws<ArgumentException>(() => DiaryTargetPolicy.Resolve(Tuesday, Owner, null,
            mondayOverride));
        Assert.Throws<ArgumentException>(() => DiaryTargetPolicy.Resolve(Monday, OtherOwner, null,
            mondayOverride));
    }

    [Fact]
    public void DtoPolicyReportsOverrideProvenanceAndRejectsNegativeValues()
    {
        var policy = new DiaryTargetPolicyUseCase();
        var effective = policy.Resolve(Owner, Monday,
            new DiaryNutritionTargetDto(1800m, 100m, 200m, 60m),
            new DiaryWeekdayOverrideDto(Owner, Monday.DayOfWeek, true,
                new DiaryNutritionTargetDto(2000m, 120m, 210m, 70m)));

        Assert.Equal(DiaryTargetProvenanceDto.UserWeekdayOverride, effective.Provenance);
        Assert.Equal(2000m, effective.Target?.Calories);
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.Resolve(Owner, Monday,
            new DiaryNutritionTargetDto(-1m, 100m, 200m, 60m), null));
    }
}
