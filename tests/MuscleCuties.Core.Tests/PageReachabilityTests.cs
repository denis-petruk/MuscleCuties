using System.Text.RegularExpressions;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
///     Measures the app's navigation graph instead of grepping for type names. Entry points are the Shell content
///     declarations, the Shell route registrations, every navigation call the app makes (GoToAsync and the navigation
///     delegates registered in the composition root, including nameof targets, literal routes and route constants) and
///     the route strings bound from XAML command parameters. Each route is then resolved to the page type it lands on,
///     so the assertions describe navigation behaviour: which route reaches which page, and which declared page no
///     route can reach.
/// </summary>
public sealed class PageReachabilityTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Every_declared_page_is_reachable_from_a_startable_route()
    {
        var map = NavigationMap.Build();

        Assert.True(map.Pages.Count >= 20, $"only {map.Pages.Count} pages parsed; the reachability map is incomplete");
        Assert.True(map.NavigationTargets.Count >= 10,
            $"only {map.NavigationTargets.Count} navigation targets found; the map cannot judge reachability");

        var unreachable = map.Pages.Where(page => !map.IsReachable(page)).ToArray();

        Assert.True(unreachable.Length == 0,
            "pages with no Shell content, no navigated route and no XAML route parameter: " +
            string.Join(", ", unreachable));
    }

    [Fact]
    public void Every_declared_route_resolves_to_a_page_and_is_navigated()
    {
        var map = NavigationMap.Build();

        var unresolved = map.Routes.Keys.Where(route => !map.Pages.Contains(route)).ToArray();
        Assert.True(unresolved.Length == 0,
            "routes that resolve to no page type: " + string.Join(", ", unresolved));

        var neverNavigated = map.RegisteredRoutes
            .Where(route => map.ShellContentPages.Contains(route) is false && !map.IsNavigated(route))
            .ToArray();
        Assert.True(neverNavigated.Length == 0,
            "registered routes nothing navigates to: " + string.Join(", ", neverNavigated));
    }

    [Fact]
    public void Every_navigation_target_resolves_to_a_declared_page()
    {
        var map = NavigationMap.Build();

        var dangling = map.NavigationTargets
            .Where(target => target is not ".." and not ".")
            .Where(target => !map.Pages.Contains(target))
            .ToArray();

        Assert.True(dangling.Length == 0,
            "navigation targets that name no declared page: " + string.Join(", ", dangling));

        var unroutable = map.NavigationTargets
            .Where(target => target is not ".." and not ".")
            .Where(target => !map.Routes.ContainsKey(target))
            .ToArray();


        Assert.True(unroutable.Length == 0,
            "navigation targets with no declared Shell route: " + string.Join(", ", unroutable));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "MuscleCuties.UI")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private sealed class NavigationMap
    {
        private static readonly Regex ShellContentRegex = new(
            "Route=\"(?<route>[^\"]+)\"\\s*\\n\\s*ContentTemplate=\"\\{DataTemplate \\w+:(?<page>\\w+)\\}\"",
            RegexOptions.Compiled);

        private static readonly Regex RegisterRouteRegex = new(
            "RegisterRoute\\(nameof\\((?<name>\\w+)\\), typeof\\((?<page>\\w+)\\)\\)", RegexOptions.Compiled);

        private static readonly Regex NavigationCallRegex = new(
            "(?:GoToAsync|NavigateToAsync|NavigateWithContextAsync|NavigateAuthenticatedAsync|ActivateRootAsync|_navigateAsync)\\s*\\(",
            RegexOptions.Compiled);

        private static readonly Regex NameOfRegex = new("nameof\\((?<name>\\w+)\\)", RegexOptions.Compiled);
        private static readonly Regex LiteralRegex = new("\"(?<route>[^\"\\\\]{2,48})\"", RegexOptions.Compiled);
        private static readonly Regex RouteConstantRegex = new(
            "const string (?<name>\\w+) = \"(?<value>[A-Za-z][A-Za-z0-9]*Page)\"", RegexOptions.Compiled);

        private NavigationMap()
        {
        }

        public Dictionary<string, string> Routes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Pages { get; } = new(StringComparer.Ordinal);
        public HashSet<string> RegisteredRoutes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ShellContentPages { get; } = new(StringComparer.Ordinal);
        public HashSet<string> NavigationTargets { get; } = new(StringComparer.Ordinal);

        // A page counts as reachable only when a declared route leads to it and something actually navigates there:
        // Shell content is entered directly, any other page needs a registered route plus a navigation target.
        public bool IsReachable(string page) =>
            ShellContentPages.Contains(page) ||
            NavigationTargets.Any(target => Routes.TryGetValue(target, out var routed) && routed == page);

        public bool IsNavigated(string route) => NavigationTargets.Contains(route);

        public static NavigationMap Build()
        {
            var map = new NavigationMap();
            var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");

            foreach (var pageFile in Directory.GetFiles(Path.Combine(uiRoot, "Pages"), "*.xaml", SearchOption.AllDirectories))
                map.Pages.Add(Path.GetFileNameWithoutExtension(pageFile));

            var shell = File.ReadAllText(Path.Combine(uiRoot, "AppShell.xaml"));
            foreach (Match match in ShellContentRegex.Matches(shell))
            {
                var route = match.Groups["route"].Value;
                var page = match.Groups["page"].Value;
                map.Routes[route] = page;
                map.RegisteredRoutes.Add(route);
                map.ShellContentPages.Add(page);
            }

            var shellCode = File.ReadAllText(Path.Combine(uiRoot, "AppShell.xaml.cs"));
            foreach (Match match in RegisterRouteRegex.Matches(shellCode))
            {
                var route = match.Groups["name"].Value;
                map.Routes[route] = match.Groups["page"].Value;
                map.RegisteredRoutes.Add(route);
            }

            var routeConstants = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
                         .Where(path => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") is false))
            {
                var text = File.ReadAllText(file);
                foreach (Match match in RouteConstantRegex.Matches(text))
                    routeConstants[match.Groups["name"].Value] = match.Groups["value"].Value;

                foreach (Match call in NavigationCallRegex.Matches(text))
                {
                    var argument = ReadArguments(text, call.Index + call.Length);

                    foreach (Match nameOf in NameOfRegex.Matches(argument))
                        map.NavigationTargets.Add(nameOf.Groups["name"].Value);

                    foreach (Match literal in LiteralRegex.Matches(argument))
                    {
                        var raw = literal.Groups["route"].Value;

                        // Interpolated routes are resolved through their nameof parts; query keys and prose are not routes.
                        if (raw.Contains('{') || raw.Contains('}'))
                            continue;

                        var route = NormalizeRoute(raw);
                        var looksLikeRoute = route.Contains("Page", StringComparison.Ordinal) ||
                                            route is ".." or "." ||
                                            raw.StartsWith('/');
                        if (looksLikeRoute)
                            map.NavigationTargets.Add(route);
                    }

                    foreach (var constant in routeConstants.Keys.Where(argument.Contains))
                        map.NavigationTargets.Add(routeConstants[constant]);
                }
            }

            // Rows in the profile surface pass the route they open as a command parameter rather than a navigation call.
            foreach (var xamlFile in Directory.GetFiles(uiRoot, "*.xaml", SearchOption.AllDirectories))
            {
                var xaml = File.ReadAllText(xamlFile);
                foreach (Match parameter in Regex.Matches(xaml, "CommandParameter=\"(?<route>\\w+Page)\""))
                    map.NavigationTargets.Add(parameter.Groups["route"].Value);
            }

            return map;
        }

        private static string NormalizeRoute(string raw)
        {
            var route = raw.Trim('/');
            var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
                route = route[..queryIndex];

            var slashIndex = route.LastIndexOf('/');
            return slashIndex >= 0 ? route[(slashIndex + 1)..] : route;
        }

        private static string ReadArguments(string text, int start)
        {
            var depth = 1;
            var index = start;
            while (index < text.Length && depth > 0)
            {
                depth += text[index] switch { '(' => 1, ')' => -1, _ => 0 };
                index++;
            }

            return text[start..Math.Max(start, index - 1)];
        }
    }
}
