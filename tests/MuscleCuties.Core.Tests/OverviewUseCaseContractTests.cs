using System.Reflection;
using MuscleCuties.UseCases.Features.Nutrition.Overview;

namespace MuscleCuties.Core.Tests;

public sealed class OverviewUseCaseContractTests
{
    [Fact]
    public void The_use_case_contract_does_not_redeclare_the_store_members()
    {
        var declared = typeof(INutritionOverviewUseCase)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Select(member => member.Name)
            .ToArray();

        Assert.Empty(declared);
    }

    [Fact]
    public void The_use_case_contract_covers_every_store_operation()
    {
        Assert.True(typeof(INutritionOverviewStore).IsAssignableFrom(typeof(INutritionOverviewUseCase)));
    }
}
