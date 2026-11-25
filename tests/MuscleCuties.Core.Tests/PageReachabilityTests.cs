using System.Text.RegularExpressions;

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
    [Fact]
    public void Every_declared_page_is_reachable_from_a_startable_route()
    {
        var map = NavigationMap.Build();

        Assert.True(map.Pages.Count >= 20, $"only {map.Pages.Count} pages parsed; the reachability map is incomplete");
        Assert.True(map.NavigationTargets.Count >= 15,
            $"only {map.NavigationTargets.Count} navigation targets found; the map cannot judge reachability");

        var unregistered = map.Pages
            .Where(page => !map.ShellContentPages.Contains(page) && !map.HasRegisteredRouteFor(page))
            .ToArray();
        Assert.True(unregistered.Length == 0,
            "pages with no Shell content and no Routing.RegisterRoute: " + string.Join(", ", unregistered));

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

    [Fact]
    public void Every_page_is_named_by_exactly_one_route()
    {
        var map = NavigationMap.Build();

        // A page that two routes reach is only reachable by accident: one of the two is a route nothing
        // navigates to, and the duplicate hides it from the checks above.
        var duplicated = map.Routes
            .GroupBy(route => route.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();

        Assert.True(duplicated.Length == 0,
            "pages declared by more than one route: " +
            string.Join(", ", duplicated.Select(group => $"{group.Key} ({string.Join("|", group.Select(r => r.Key))})")));
    }

    private sealed class NavigationMap
    {
        private static readonly Regex ShellContentBlockRegex = new(
            "<ShellContent(?<attributes>[^>]*)>", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex AttributeRegex = new(
            "(?<name>Route|ContentTemplate)=\"(?<value>[^\"]*)\"", RegexOptions.Compiled);

        private static readonly Regex DataTemplateTypeRegex = new(
            @"\{\s*DataTemplate\s+\w+:(?<page>\w+)\s*\}", RegexOptions.Compiled);

        private static readonly Regex RegisterRouteRegex = new(
            @"RegisterRoute\(\s*nameof\((?<route>\w+)\)\s*,\s*typeof\((?<page>\w+)\)\s*\)", RegexOptions.Compiled);

        private static readonly Regex NavigationCallRegex = new(
            @"(?<method>GoToAsync|NavigateToAsync|NavigateAuthenticatedAsync|NavigateWithContextAsync|" +
            @"NavigateThenPreloadDashboardAsync|ActivateRootAsync|_navigateAsync)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex NameOfRegex = new(@"nameof\((?<name>\w+)\)", RegexOptions.Compiled);
        private static readonly Regex LiteralRegex = new("\"(?<route>[^\"\\\\]{2,64})\"", RegexOptions.Compiled);
        private static readonly Regex RouteConstantRegex = new(
            @"const\s+string\s+(?<name>\w+)\s*=\s*""(?<value>[\w/]+Page)""", RegexOptions.Compiled);
        private static readonly Regex IdentifierRegex = new(@"\b(?<name>[A-Z]\w*Page)\b", RegexOptions.Compiled);

        private static readonly Regex XamlRouteParameterRegex = new(
            "CommandParameter=\"(?<route>\\w+Page)\"", RegexOptions.Compiled);

        private static readonly Regex XamlCommentRegex = new("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly string _repoRoot;

        private NavigationMap(string repoRoot)
        {
            _repoRoot = repoRoot;
        }

        /// <summary>Route name to the page type it presents.</summary>
        public Dictionary<string, string> Routes { get; } = new(StringComparer.Ordinal);

        /// <summary>Every page type that ships with a XAML file.</summary>
        public HashSet<string> Pages { get; } = new(StringComparer.Ordinal);

        /// <summary>Routes declared by the Shell, either as content or through Routing.RegisterRoute.</summary>
        public HashSet<string> RegisteredRoutes { get; } = new(StringComparer.Ordinal);

        /// <summary>Pages the Shell presents as content, which are entered directly.</summary>
        public HashSet<string> ShellContentPages { get; } = new(StringComparer.Ordinal);

        /// <summary>Routes the app actually navigates to, in any spelling.</summary>
        public HashSet<string> NavigationTargets { get; } = new(StringComparer.Ordinal);

        // A page counts as reachable only when a declared route leads to it and something actually navigates there:
        // Shell content is entered directly, any other page needs a registered route plus a navigation target.
        public bool IsReachable(string page) =>
            ShellContentPages.Contains(page) ||
            NavigationTargets.Any(target => Routes.TryGetValue(target, out var routed) && routed == page);

        public bool HasRegisteredRouteFor(string page) =>
            Routes.Any(route => route.Value == page);

        public bool IsNavigated(string route) => NavigationTargets.Contains(route);

        public static NavigationMap Build()
        {
            var map = new NavigationMap(FindRepoRoot());
            map.ReadPages();
            map.ReadShellContent();
            map.ReadRegisteredRoutes();
            map.ReadNavigatedRoutes();
            map.ReadXamlRouteParameters();
            return map;
        }

        private string UiRoot => Path.Combine(_repoRoot, "src", "MuscleCuties.UI");

        private void ReadPages()
        {
            foreach (var pageFile in Directory.GetFiles(Path.Combine(UiRoot, "Pages"), "*.xaml",
                         SearchOption.AllDirectories))
                Pages.Add(Path.GetFileNameWithoutExtension(pageFile));
        }

        private void ReadShellContent()
        {
            var shell = File.ReadAllText(Path.Combine(UiRoot, "AppShell.xaml"));
            foreach (Match block in ShellContentBlockRegex.Matches(shell))
            {
                var route = string.Empty;
                var page = string.Empty;
                foreach (Match attribute in AttributeRegex.Matches(block.Groups["attributes"].Value))
                {
                    if (attribute.Groups["name"].Value == "Route")
                    {
                        route = attribute.Groups["value"].Value;
                        continue;
                    }

                    var pageMatch = DataTemplateTypeRegex.Match(attribute.Groups["value"].Value);
                    if (pageMatch.Success)
                        page = pageMatch.Groups["page"].Value;
                }

                if (route.Length == 0 || page.Length == 0)
                    continue;

                Routes[route] = page;
                RegisteredRoutes.Add(route);
                ShellContentPages.Add(page);
            }
        }

        private void ReadRegisteredRoutes()
        {
            var shellCode = File.ReadAllText(Path.Combine(UiRoot, "AppShell.xaml.cs"));
            foreach (Match match in RegisterRouteRegex.Matches(shellCode))
            {
                // A commented-out registration is not a registration: counting one would let a route
                // whose only declaration was disabled keep a page looking reachable.
                if (IsCommented(shellCode, match.Index))
                    continue;

                var route = match.Groups["route"].Value;
                Routes[route] = match.Groups["page"].Value;
                RegisteredRoutes.Add(route);
            }
        }

        private void ReadNavigatedRoutes()
        {
            var routeConstants = new Dictionary<string, string>(StringComparer.Ordinal);
            var sources = Directory.GetFiles(UiRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") is false)
                .Select(path => File.ReadAllText(path))
                .ToArray();

            foreach (var text in sources)
            foreach (Match match in RouteConstantRegex.Matches(text))
                routeConstants[match.Groups["name"].Value] = match.Groups["value"].Value;

            foreach (var text in sources)
            foreach (Match call in NavigationCallRegex.Matches(text))
            {
                if (IsCommented(text, call.Index))
                    continue;

                var argument = ReadArguments(text, call.Index + call.Length);

                foreach (Match nameOf in NameOfRegex.Matches(argument))
                    NavigationTargets.Add(nameOf.Groups["name"].Value);

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
                        NavigationTargets.Add(route);
                }

                // A route that is only ever named through a shared constant still counts as navigated.
                foreach (var constant in routeConstants.Where(constant => argument.Contains(constant.Key)))
                    NavigationTargets.Add(constant.Value);

                foreach (Match identifier in IdentifierRegex.Matches(argument))
                    NavigationTargets.Add(identifier.Groups["name"].Value);
            }
        }

        private void ReadXamlRouteParameters()
        {
            // Rows in the profile surface pass the route they open as a command parameter rather than a navigation call.
            foreach (var xamlFile in Directory.GetFiles(UiRoot, "*.xaml", SearchOption.AllDirectories))
            {
                var xaml = XamlCommentRegex.Replace(File.ReadAllText(xamlFile), string.Empty);
                foreach (Match parameter in XamlRouteParameterRegex.Matches(xaml))
                    NavigationTargets.Add(parameter.Groups["route"].Value);
            }
        }

        /// <summary>
        ///     Reports whether the construct at <paramref name="index" /> sits behind a line comment, so a
        ///     disabled registration or navigation call cannot keep a dead page looking reachable.
        /// </summary>
        private static bool IsCommented(string text, int index)
        {
            var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            var prefix = text[lineStart..index];
            var comment = prefix.IndexOf("//", StringComparison.Ordinal);
            if (comment < 0)
                return false;

            // A "//" inside a string literal is part of the route ("//LoginPage"), not a comment.
            var quotes = prefix.Count(character => character == '"');
            return quotes % 2 == 0 && !prefix[(comment + 2)..].Contains('"');
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

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "MuscleCuties.UI")))
                directory = directory.Parent;

            return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }
}
