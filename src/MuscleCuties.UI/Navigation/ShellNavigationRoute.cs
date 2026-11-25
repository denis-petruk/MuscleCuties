namespace MuscleCuties.UI.Navigation;

/// <summary>
/// The single route-shaping boundary for Shell navigation. A query is part of the route: pages
/// such as the food diary read entryId through IQueryAttributable. Only the login-root
/// comparison and root activation deliberately use the query-free form.
/// </summary>
internal static class ShellNavigationRoute
{
    internal const string LoginRoot = "//LoginPage";

    /// <summary>The route as the shell expects it.</summary>
    public static string ForShell(string route) => route;

    /// <summary>True when the route is the login root.</summary>
    public static bool IsLoginRoot(string route) => WithoutQuery(route) == LoginRoot;

    /// <summary>The route up to its query separator. Only root comparisons use this form, because a</summary>
    /// query is otherwise part of the route.</summary>
    public static string WithoutQuery(string route)
    {
        var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        return queryIndex < 0 ? route : route[..queryIndex];
    }
}
