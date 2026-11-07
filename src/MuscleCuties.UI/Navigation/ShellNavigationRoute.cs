namespace MuscleCuties.UI.Navigation;

/// <summary>
/// The single route-shaping boundary for Shell navigation. A query is part of the route: pages
/// such as the food diary read entryId/addMeal/addAt through IQueryAttributable. Only the login-root
/// comparison and root activation deliberately use the query-free form.
/// </summary>
internal static class ShellNavigationRoute
{
    internal const string LoginRoot = "//LoginPage";

    public static string ForShell(string route) => route;

    public static bool IsLoginRoot(string route) => WithoutQuery(route) == LoginRoot;

    public static string WithoutQuery(string route)
    {
        var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        return queryIndex < 0 ? route : route[..queryIndex];
    }
}
