using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.UI.Navigation;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class MealNavigationContractTests
{
    [Fact]
    public void Logged_meal_route_keeps_entryId_through_shell_navigation()
    {
        var historyId = Guid.NewGuid();
        var route = NutritionNavigation.ToLogEntry(historyId);

        var shellRoute = ShellNavigationRoute.ForShell(route);

        Assert.Equal(route, shellRoute);
        Assert.True(NutritionNavigation.TryReadEntryId(ParseQuery(shellRoute), out var parsed));
        Assert.Equal(historyId, parsed);
    }

    [Fact]
    public void Add_food_route_keeps_addMeal_and_addAt_through_shell_navigation()
    {
        var route = NutritionNavigation.ToAddFood(MealType.Dinner, 18 * 60 + 30);

        var shellRoute = ShellNavigationRoute.ForShell(route);

        Assert.Equal(route, shellRoute);
        Assert.True(NutritionNavigation.TryReadAddFoodIntent(
            ParseQuery(shellRoute), out var mealTypeName, out var minuteOfDay));
        Assert.Equal(nameof(MealType.Dinner), mealTypeName);
        Assert.Equal(18 * 60 + 30, minuteOfDay);
    }

    [Theory]
    [InlineData("//NutritionPage")]
    [InlineData("FoodLogPage")]
    [InlineData("..")]
    public void Routes_without_queries_are_unchanged(string route)
    {
        Assert.Equal(route, ShellNavigationRoute.ForShell(route));
    }

    [Fact]
    public void Login_root_comparison_ignores_the_query_without_stripping_the_shell_route()
    {
        const string route = "//LoginPage?source=expired";

        Assert.True(ShellNavigationRoute.IsLoginRoot(route));
        Assert.Equal(route, ShellNavigationRoute.ForShell(route));
        Assert.Equal("//LoginPage", ShellNavigationRoute.WithoutQuery(route));
        Assert.False(ShellNavigationRoute.IsLoginRoot("//DashboardPage?source=expired"));
    }

    [Fact]
    public void Chooser_cannot_close_before_the_editor_has_been_presented()
    {
        Assert.True(NutritionMealEntryHandoff.IsHandoff(
            editorRequested: true, chooserVisible: true));
        Assert.False(NutritionMealEntryHandoff.CanCloseChooser(
            editorRequested: true, chooserVisible: true, editorPresented: false));
        Assert.True(NutritionMealEntryHandoff.CanCloseChooser(
            editorRequested: true, chooserVisible: true, editorPresented: true));
    }

    [Fact]
    public void Chooser_is_not_closed_without_a_requested_handoff()
    {
        Assert.False(NutritionMealEntryHandoff.IsHandoff(
            editorRequested: false, chooserVisible: true));
        Assert.False(NutritionMealEntryHandoff.CanCloseChooser(
            editorRequested: false, chooserVisible: true, editorPresented: true));
        Assert.False(NutritionMealEntryHandoff.CanCloseChooser(
            editorRequested: true, chooserVisible: false, editorPresented: true));
    }

    private static Dictionary<string, object> ParseQuery(string route)
    {
        var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
            return new Dictionary<string, object>(StringComparer.Ordinal);

        return route[(queryIndex + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => (object)Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : string.Empty),
                StringComparer.Ordinal);
    }
}
