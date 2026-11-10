using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Guards the two UI conventions that the compiled backend cannot otherwise protect: every
/// user-facing string resolves through the localization catalog, and every page route or query
/// key comes from a navigation contract. Both scans read source text, so a regression fails the
/// backend suite instead of waiting for a manual grep.
/// </summary>
public sealed class UiContractGuardTests
{
    private static readonly Regex VisibleTextAttribute = new(
        "(?:Text|Title|Placeholder)=\"(?<value>[^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ContractBackedValue = new(
        @"^\{\s*(?:loc:|x:Static|Binding|StaticResource|DynamicResource)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Only a quoted string can be a route; nullable annotations such as `AppStartupPage?` are types.
    private static readonly Regex PageRouteLiteral = new(
        "\"[^\"\\n]*Page\\?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] QueryKeys = ["\"entryId\"", "\"addMeal\"", "\"addAt\""];

    // A semantic override names a colour pair its typographic style does not already set.
    private static readonly Regex ThemeColourPair = new(
        @"Light=\{StaticResource\s+(?<light>[A-Za-z0-9_]+)\}\s*,\s*Dark=\{StaticResource\s+(?<dark>[A-Za-z0-9_]+)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StaticResourceReference = new(
        @"^\{\s*StaticResource\s+(?<key>[A-Za-z0-9_]+)\s*\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string ImplicitLabelStyle = "implicit-label";

    // A raw palette name encodes a colour value rather than an intent. Existing definitions stay
    // for compatibility, but no consumer may resolve one and no new one may be introduced.
    private static readonly Regex ValueShapedColourName = new(
        @"^(?:Gray|Grey)\d+$|^(?:White|Black|DeepMauve)$|(?:Pink|Blue|Green|Red|Mauve|Purple|Orange|Yellow|Brown|Teal|Navy|Cyan|Magenta|Rose)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PaletteReference = new(
        @"StaticResource\s+(?<key>[A-Za-z0-9_]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex QuotedPaletteKey = new(
        "\"(?<key>[A-Za-z0-9_]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] KnownValueShapedPaletteTokens =
    [
        "Gray100", "Gray200", "Gray300", "Gray400", "Gray500",
        "Gray600", "Gray700", "Gray800", "Gray900", "Gray950",
        "White", "Black", "DeepMauve", "DashboardAccentPink", "DashboardReadinessBlue"
    ];

    [Fact]
    public void Path_data_bindings_convert_string_paths_to_geometry()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            foreach (var line in File.ReadLines(file))
            {
                // Ignore bindable-property names that merely end in "Data" (e.g. MealTypeIconData);
                // only a Path's own Data attribute holds a Geometry.
                if (!Regex.IsMatch(line, "(?<![A-Za-z0-9_])Data=\"\\{Binding", RegexOptions.CultureInvariant))
                    continue;

                if (!line.Contains("StringToGeometryConverter", StringComparison.Ordinal))
                    violations.Add($"{Path.GetRelativePath(uiRoot, file)}: {line.Trim()}");
            }
        }

        Assert.True(violations.Count == 0,
            "Path.Data bindings must convert string path data through StringToGeometryConverter:\n" +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Page_and_shell_xaml_carry_no_literal_visible_text()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            foreach (Match match in VisibleTextAttribute.Matches(File.ReadAllText(file)))
            {
                var value = match.Groups["value"].Value;
                if (ContractBackedValue.IsMatch(value))
                    continue;

                // Separators and icon glyphs ("·", "!", a spacing span) carry no language; anything
                // with a letter or a digit is copy, a unit or a placeholder and must be catalogued.
                if (!value.Any(char.IsLetterOrDigit))
                    continue;

                violations.Add($"{Relative(file, uiRoot)}: {match.Value}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} literal user-facing string(s) in XAML. Resolve each through the catalog:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Nutrition_presentation_does_not_reference_core_or_use_case_contracts()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var roots = new[]
        {
            Path.Combine(uiRoot, "Pages", "Nutrition"),
            Path.Combine(uiRoot, "Controls", "Nutrition"),
            Path.Combine(uiRoot, "Models", "UI", "Nutrition"),
            Path.Combine(uiRoot, "ViewModels", "Nutrition"),
            Path.Combine(uiRoot, "Services", "Nutrition")
        };
        var explicitFiles = new[]
        {
            Path.Combine(uiRoot, "Navigation", "NutritionNavigation.cs"),
            Path.Combine(uiRoot, "Resources", "Converters", "FoodLogConverters.cs")
        };
        var prohibited = new Regex(
            @"\b(?:[A-Z][A-Za-z0-9_]*(?:Dto|DTO)|MacroNutrients|MealNutritionTarget|NutritionPlan)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var violations = new List<string>();
        var files = roots.SelectMany(root => SourceFiles(root, "*.cs"))
            .Concat(explicitFiles.Where(File.Exists))
            .Where(file => !Path.GetFileName(file).StartsWith("NutritionPresentationMapper", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);

        foreach (var file in files)
        {
            foreach (Match match in prohibited.Matches(File.ReadAllText(file)))
            {
                violations.Add(
                    $"{Relative(file, uiRoot).Replace('\\', '/')} references Core or UseCases nutrition contract '{match.Value}'. Map it through NutritionPresentationMapper.");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} Core or UseCases nutrition contract reference(s) in Nutrition presentation files:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Presentation_xaml_does_not_reference_infrastructure_assemblies()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();
        var prohibited = new[]
        {
            "assembly=MuscleCuties.Infrastructure",
            "clr-namespace:MuscleCuties.Infrastructure"
        };

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var content = File.ReadAllText(file);
            if (prohibited.Any(token => content.Contains(token, StringComparison.OrdinalIgnoreCase)))
                violations.Add(Relative(file, uiRoot).Replace(Path.DirectorySeparatorChar, '/'));
        }

        Assert.True(
            violations.Count == 0,
            "Presentation XAML must not reference Infrastructure assemblies:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Shared_page_header_uses_page_title_and_level_one_heading()
    {
        var path = Path.Combine(
            RepositoryRoot(), "src", "MuscleCuties.UI", "Controls", "Shared", "PageHeader.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("SetDynamicResource(StyleProperty, \"PageTitle\")", source);
        Assert.Contains("SetHeadingLevel(title, SemanticHeadingLevel.Level1)", source);
    }

    [Fact]
    public void Accessibility_hints_and_descriptions_use_localized_or_bound_values()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();
        var semanticProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "SemanticProperties.Description",
            "SemanticProperties.Hint"
        };

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var element in document.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    string value;
                    if (semanticProperties.Contains(attribute.Name.LocalName))
                    {
                        value = attribute.Value;
                    }
                    else if (element.Name.LocalName == "Setter" &&
                             semanticProperties.Contains(element.Attribute("Property")?.Value ?? string.Empty) &&
                             attribute.Name.LocalName == "Value")
                    {
                        value = attribute.Value;
                    }
                    else
                    {
                        continue;
                    }

                    if (value.StartsWith("{", StringComparison.Ordinal) || !value.Any(char.IsLetterOrDigit))
                        continue;

                    var line = ((IXmlLineInfo)element).LineNumber;
                    violations.Add(
                        $"{Relative(file, uiRoot).Replace('\\', '/')}:{line} has literal accessibility copy '{value}'. Use a catalog key or a localized binding.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} literal accessibility string(s). Resolve each through the catalog:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Route_formats_and_query_keys_live_only_in_a_navigation_contract()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.cs"))
        {
            var relative = Relative(file, uiRoot).Replace('\\', '/');
            if (relative.Contains("Navigation/", StringComparison.Ordinal))
                continue;

            var source = File.ReadAllText(file);

            if (PageRouteLiteral.IsMatch(source))
                violations.Add($"{relative}: builds a page route with a literal query string");

            foreach (var key in QueryKeys)
            {
                if (source.Contains(key, StringComparison.Ordinal))
                    violations.Add($"{relative}: repeats the navigation query key {key}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} route/query literal(s) outside Navigation/. Move them into the feature's contract:\n"
            + string.Join("\n", violations));
    }

    /// <summary>
    /// Mirrors the sanctioned colour-exception table in DESIGN.md. A site may only be added by
    /// updating that table, so the documentation and the markup cannot drift apart.
    /// </summary>
    [Fact]
    public void Inline_text_colours_match_the_documented_exception_list()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Pages/Nutrition/FoodLogPage.xaml"] = 7,
            ["Pages/Workout/WorkoutHistoryPage.xaml"] = 5,
            ["Pages/Cycle/CyclePhaseDetailPage.xaml"] = 3,
            ["Pages/Workout/WorkoutPage.xaml"] = 2,
            ["Pages/Profile/ProfilePage.xaml"] = 1,
            ["Pages/Workout/StrengthPlanGenerationPage.xaml"] = 1,
        };

        var inlineColour = new Regex("TextColor=\"\\{AppThemeBinding", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var actual = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var relative = Relative(file, uiRoot).Replace('\\', '/');
            if (!relative.StartsWith("Pages/", StringComparison.Ordinal))
                continue;

            var count = inlineColour.Matches(File.ReadAllText(file)).Count;
            if (count > 0)
                actual[relative] = count;
        }

        var mismatchedPaths = expected.Keys.Union(actual.Keys)
            .Where(path => expected.GetValueOrDefault(path) != actual.GetValueOrDefault(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var drift = mismatchedPaths
            .Select(path => $"{path}: DESIGN.md records {expected.GetValueOrDefault(path)}, markup has {actual.GetValueOrDefault(path)}")
            .ToList();

        Assert.True(
            mismatchedPaths.Count == 0,
            $"{mismatchedPaths.Count} file(s) disagree with the sanctioned colour exceptions in DESIGN.md:\n"
            + string.Join("\n", drift));

        // Counting sites cannot prove they are semantic: a duplicate can replace an exception and
        // keep the total, so each site is resolved against the style it actually applies. An
        // override that repeats its own style's pair (including through BasedOn and the implicit
        // label style) is the duplicate DESIGN.md forbids, not an exception.
        var styles = ReadLabelTextColours(Path.Combine(uiRoot, "Resources", "Styles", "Styles.xaml"));
        var duplicateColourSites = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var relative = Relative(file, uiRoot).Replace('\\', '/');
            if (!relative.StartsWith("Pages/", StringComparison.Ordinal))
                continue;

            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var element in document.Descendants())
            {
                var textColour = element.Attribute("TextColor")?.Value;
                var styleName = AppliedStyleName(element);
                if (textColour is null || styleName is null)
                    continue;

                var pair = ThemeColourPair.Match(textColour);
                var own = pair.Success ? ResolveTextColour(styles, styleName) : null;
                if (own is not { } inherited ||
                    inherited.Light != pair.Groups["light"].Value ||
                    inherited.Dark != pair.Groups["dark"].Value)
                {
                    continue;
                }

                var line = ((IXmlLineInfo)element).LineNumber;
                duplicateColourSites.Add(
                    $"{relative}:{line} repeats {inherited.Light}/{inherited.Dark}, already set by {styleName}");
            }
        }

        Assert.True(
            duplicateColourSites.Count == 0,
            $"{duplicateColourSites.Count} inline colour(s) repeat the pair their own style already sets:\n"
            + string.Join("\n", duplicateColourSites));
    }

    [Fact]
    public void Page_and_control_xaml_use_tokens_for_dimensions()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var numericDimension = new Regex(
            "(?:WidthRequest|HeightRequest|FontSize|CornerRadius)=\"(?<value>[0-9]+)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var violations = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var relative = Relative(file, uiRoot).Replace('\\', '/');
            if (relative.StartsWith("Resources/", StringComparison.Ordinal))
                continue;

            foreach (Match match in numericDimension.Matches(File.ReadAllText(file)))
                violations.Add($"{relative}: {match.Value} (use a UiDimension/UiFontSize token)");
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} raw numeric dimension(s) outside the token catalog:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Value_shaped_colour_tokens_are_definition_only()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var colorsPath = Path.Combine(uiRoot, "Resources", "Styles", "Colors.xaml");
        var paletteKeys = XDocument.Load(colorsPath).Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .Select(element => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);

        var newValueShaped = paletteKeys
            .Where(key => ValueShapedColourName.IsMatch(key))
            .Where(key => !KnownValueShapedPaletteTokens.Contains(key, StringComparer.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            newValueShaped.Length == 0,
            $"Colors.xaml adds {newValueShaped.Length} value-shaped colour token(s): {string.Join(", ", newValueShaped)}. " +
            "Define an intent-named role with the same colour instead of a new raw value.");

        var violations = new List<string>();
        foreach (var file in SourceFiles(uiRoot, "*.xaml").Concat(SourceFiles(uiRoot, "*.cs")))
        {
            var relative = Relative(file, uiRoot).Replace('\\', '/');
            if (relative == "Resources/Styles/Colors.xaml")
                continue;

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
                FlagValueShapedPaletteReferences(relative, index + 1, lines[index], paletteKeys, violations);
        }

        Assert.True(
            violations.Count == 0,
            $"{violations.Count} value-shaped colour reference(s) outside Colors.xaml. Use the role token for the same intent:\n"
            + string.Join("\n", violations));
    }

    private static void FlagValueShapedPaletteReferences(
        string relative, int lineNumber, string text, HashSet<string> paletteKeys, List<string> violations)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith("<!--", StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in PaletteReference.Matches(text))
            Flag(match.Groups["key"].Value);
        foreach (Match match in QuotedPaletteKey.Matches(text))
            Flag(match.Groups["key"].Value);

        void Flag(string key)
        {
            if (paletteKeys.Contains(key) && ValueShapedColourName.IsMatch(key))
            {
                violations.Add(
                    $"{relative}:{lineNumber} references value-shaped colour '{key}'. Raw palette keys are definition-only; use the intent-named role.");
            }
        }
    }

    /// <summary>
    /// A modal is not allowed to build its own scrim/surface outside the shared control. The one
    /// exception is the non-dismissible day-overview state inside WorkoutSessionPage; it has no
    /// close/backdrop action and is not a sheet, and is recorded in the dated audit.
    /// </summary>
    [Fact]
    public void Page_local_modal_chrome_lives_inside_the_shared_modal_control()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var relative = Relative(file, uiRoot).Replace('\\', '/');
            if (relative.StartsWith("Resources/", StringComparison.Ordinal))
                continue;

            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            var elements = document.Root is null
                ? Enumerable.Empty<XElement>()
                : document.Root.DescendantsAndSelf();

            foreach (var element in elements)
            {
                if (!UsesModalChrome(element))
                    continue;

                if (element.AncestorsAndSelf().Any(ancestor => ancestor.Name.LocalName == "AnimatedModalView"))
                    continue;

                if (relative == "Pages/Workout/WorkoutSessionPage.xaml" &&
                    element.Ancestors().Any(ancestor =>
                        ancestor.Attribute("IsVisible")?.Value == "{Binding IsDayOverview}"))
                {
                    continue;
                }

                var line = ((IXmlLineInfo)element).LineNumber;
                violations.Add(
                    $"{relative}:{line} uses modal chrome outside AnimatedModalView. Present it through the shared sheet contract or record a non-modal exception in the audit.");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"{violations.Count} modal surface(s) bypass the shared sheet contract:\n"
            + string.Join("\n", violations));
    }

    private static bool UsesModalChrome(XElement element)
    {
        var style = element.Attribute("Style")?.Value ?? string.Empty;
        var background = element.Attribute("BackgroundColor")?.Value ?? string.Empty;
        return style.Contains("ModalBackdrop", StringComparison.Ordinal) ||
               style.Contains("ModalSurface", StringComparison.Ordinal) ||
               background.Contains("ModalScrim", StringComparison.Ordinal);
    }

    /// <summary>
    /// The shared sheet is the single place that owns focus, background isolation and the title
    /// announcement. This is a source contract because the Core test project cannot host MAUI
    /// controls; removing any part of the lifecycle fails here instead of regressing silently.
    /// </summary>
    [Fact]
    public void Shared_modals_isolate_background_restore_focus_and_announce_a_heading()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var modalSource = File.ReadAllText(
            Path.Combine(uiRoot, "Controls", "Shared", "AnimatedModalView.cs"));

        Assert.Contains("AutomationProperties.ExcludedWithChildrenProperty", modalSource);
        Assert.Contains("FocusedOutsideSheet", modalSource);
        Assert.Contains("ModalAccessibilityScope.Acquire", modalSource);
        Assert.Contains("ModalAccessibilityScope.Release", modalSource);
        Assert.Contains("SemanticScreenReader.Default.Announce", modalSource);
        Assert.Contains("_transitionVersion", modalSource);

        var transitionSource = File.ReadAllText(
            Path.Combine(uiRoot, "Controls", "Shared", "ModalTransition.cs"));
        Assert.Contains("MotionSettings.ReduceMotion", transitionSource);
        Assert.Contains("modal.CancelAnimations();", transitionSource);
        Assert.Contains("FindCard(modal)?.CancelAnimations();", transitionSource);

        var stylesSource = File.ReadAllText(
            Path.Combine(uiRoot, "Resources", "Styles", "Styles.xaml"));
        var titleStyle = Regex.Match(stylesSource, "<Style x:Key=\"ModalTitle\"[\\s\\S]*?</Style>");
        Assert.True(titleStyle.Success, "ModalTitle style is missing from Styles.xaml.");
        Assert.Contains("SemanticProperties.HeadingLevel", titleStyle.Value);

        var missingTitle = new List<string>();
        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var document = XDocument.Load(file);
            if (document.Root?.Name.LocalName != "AnimatedModalView")
                continue;

            var hasHeading = document.Descendants().Any(element =>
                element.Name.LocalName == "Label" &&
                (element.Attribute("Style")?.Value.Contains("ModalTitle", StringComparison.Ordinal) == true ||
                 element.Attribute("SemanticProperties.HeadingLevel") is not null));
            if (!hasHeading)
                missingTitle.Add(Relative(file, uiRoot));
        }

        Assert.True(
            missingTitle.Count == 0,
            $"Found {missingTitle.Count} shared modal(s) without a ModalTitle heading to announce:\n"
            + string.Join("\n", missingTitle));
    }

    [Fact]
    public void Full_page_lazy_modal_hosts_keep_page_binding_context_and_input_transparency()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var nutritionXaml = XDocument.Load(
            Path.Combine(uiRoot, "Pages", "Nutrition", "NutritionPage.xaml"),
            LoadOptions.SetLineInfo);

        var overlayLazyViews = nutritionXaml
            .Descendants()
            .Where(element => element.Name.LocalName == "LazyView" &&
                              element.Attribute("Grid.RowSpan") is not null)
            .ToArray();

        Assert.NotEmpty(overlayLazyViews);
        foreach (var lazyView in overlayLazyViews)
        {
            var name = lazyView.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2009/xaml"))?.Value
                       ?? "(unnamed)";
            Assert.Null(lazyView.Attribute("BindingContext"));
            Assert.NotNull(lazyView.Attribute("IsVisible"));
            Assert.NotNull(lazyView.Attribute("InputTransparent"));
            Assert.False(string.IsNullOrWhiteSpace(name));
        }
    }

    [Fact]
    public void Registered_pages_have_an_entry_point_and_a_back_path()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var appShell = File.ReadAllText(Path.Combine(uiRoot, "AppShell.xaml.cs"));
        var registration = new Regex(
            @"Routing\.RegisterRoute\s*\(\s*nameof\s*\(\s*(?<page>[A-Za-z0-9_]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var pages = registration.Matches(appShell).Select(match => match.Groups["page"].Value)
            .Distinct(StringComparer.Ordinal).OrderBy(page => page, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(pages);

        var allFiles = SourceFiles(uiRoot, "*.cs").Concat(SourceFiles(uiRoot, "*.xaml")).ToArray();
        var backSignals = new[] { "BackCommand", "GoBackCommand", "NavigateBackCommand", "CloseCommand", "CancelCommand" };
        var violations = new List<string>();

        foreach (var page in pages)
        {
            var pageFile = allFiles.FirstOrDefault(file =>
                file.EndsWith($"{Path.DirectorySeparatorChar}{page}.xaml", StringComparison.Ordinal));
            if (pageFile is null)
            {
                violations.Add($"{page}: registered route has no page XAML");
                continue;
            }

            var pageXaml = File.ReadAllText(pageFile);
            var pageCode = File.Exists(pageFile + ".cs") ? File.ReadAllText(pageFile + ".cs") : string.Empty;
            var hidesNativeBar = pageXaml.Contains("Shell.NavBarIsVisible=\"False\"", StringComparison.Ordinal);
            var hasBack = backSignals.Any(signal => pageXaml.Contains(signal, StringComparison.Ordinal)) ||
                          pageCode.Contains("OnBackButtonPressed", StringComparison.Ordinal);
            if (hidesNativeBar && !hasBack)
                violations.Add($"{Relative(pageFile, uiRoot)}: hides the Shell navigation bar without a back/close command");

            var hasEntry = allFiles
                .Where(file => !string.Equals(file, pageFile, StringComparison.Ordinal) &&
                               !string.Equals(file, pageFile + ".cs", StringComparison.Ordinal))
                .Where(file => !Relative(file, uiRoot).Replace('\\', '/').StartsWith("Pages/", StringComparison.Ordinal))
                .Any(file =>
                {
                    var text = File.ReadAllText(file);
                    if (file.EndsWith(".xaml", StringComparison.Ordinal))
                        return text.Contains($"CommandParameter=\"{page}\"", StringComparison.Ordinal);

                    return text.Contains($"nameof({page})", StringComparison.Ordinal) ||
                           (Relative(file, uiRoot).Replace('\\', '/').StartsWith("Navigation/", StringComparison.Ordinal) &&
                            text.Contains($"\"{page}\"", StringComparison.Ordinal));
                });
            if (!hasEntry)
                violations.Add($"{page}: registered route has no reachable entry point outside its own page");
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} navigation reachability/return defect(s):\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Workout_header_uses_the_shared_status_slot()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var headerSource = File.ReadAllText(
            Path.Combine(uiRoot, "Controls", "Shared", "PageHeader.cs"));
        var workoutSource = File.ReadAllText(
            Path.Combine(uiRoot, "Pages", "Workout", "WorkoutPage.xaml"));

        Assert.Contains("StatusContentProperty", headerSource);
        Assert.Contains("<sharedControls:PageHeader", workoutSource);
        Assert.Contains("PageHeader.StatusContent", workoutSource);
        Assert.Contains("PillBadge", workoutSource);
    }

    /// <summary>
    /// S01: an icon-only header action carries no visible label, so its accessible name is the only
    /// way VoiceOver can describe it. The shared style cannot supply a per-page name, so every
    /// instance declares one from the catalog. .NET MAUI 10 deprecates AutomationProperties.Name in
    /// favour of SemanticProperties.Description, so that property is the contract enforced here.
    /// </summary>
    [Fact]
    public void Header_icon_buttons_declare_a_catalog_backed_accessible_name()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var element in document.Descendants())
            {
                if (element.Attribute("Style")?.Value.Contains("HeaderIconButton", StringComparison.Ordinal) != true)
                    continue;

                var name = element.Attribute("SemanticProperties.Description")?.Value;
                if (name is not null && name.StartsWith("{", StringComparison.Ordinal))
                    continue;

                var line = ((IXmlLineInfo)element).LineNumber;
                violations.Add(
                    $"{Relative(file, uiRoot).Replace('\\', '/')}:{line} uses HeaderIconButton without a catalog-backed SemanticProperties.Description");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} header icon button(s) VoiceOver cannot name:\n"
            + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// AutomationProperties.Name is obsolete in .NET MAUI 10 and maps to the same iOS label as
    /// SemanticProperties.Description. Setting both restates one name and starts a second,
    /// unsupported accessibility contract, so the deprecated property must not reappear.
    /// </summary>
    [Fact]
    public void Deprecated_automation_name_does_not_replace_the_semantic_property()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var violations = new List<string>();

        foreach (var file in SourceFiles(uiRoot, "*.xaml"))
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var attribute in document.Descendants().Attributes())
            {
                if (!string.Equals(attribute.Name.LocalName, "AutomationProperties.Name", StringComparison.Ordinal))
                    continue;

                var line = ((IXmlLineInfo)attribute).LineNumber;
                violations.Add(
                    $"{Relative(file, uiRoot).Replace('\\', '/')}:{line} uses the deprecated AutomationProperties.Name; use SemanticProperties.Description");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} deprecated accessibility name(s):\n"
            + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// F01: an unanswered clinician-advice answer, an explicit Yes and an explicit No are three
    /// meanings. A two-state control collapses Unanswered into No, so the form must offer all three
    /// through the shared choice anatomy and keep the null answer distinct.
    /// </summary>
    [Fact]
    public void Injury_form_offers_three_distinct_clinician_advice_answers()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var content = File.ReadAllText(
            Path.Combine(uiRoot, "Controls", "Injury", "InjuryLogContent.xaml"));
        var viewModel = File.ReadAllText(
            Path.Combine(uiRoot, "ViewModels", "Workout", "InjuryLogViewModel.cs"));

        Assert.Contains("BindableLayout.ItemsSource=\"{Binding ClinicianAdviceOptions}\"", content);
        Assert.Contains("SelectClinicianAdviceOptionCommand", content);
        Assert.DoesNotContain("<Switch", content);

        Assert.Contains("OnSelectedClinicianAdviceChanged(bool? value)", viewModel);
        Assert.Contains("Value = null, LabelKey = \"Injury_Log_ClinicianAdvice_Unanswered\"", viewModel);
        Assert.Contains("Value = true, LabelKey = \"Injury_Log_ClinicianAdvice_Yes\"", viewModel);
        Assert.Contains("Value = false, LabelKey = \"Injury_Log_ClinicianAdvice_No\"", viewModel);
    }

    /// <summary>
    /// The chooser and the editor are two full-screen layers. Closing the chooser before the editor
    /// has completed its open transition exposes the page between them, so the source order is a
    /// contract, not an implementation detail.
    /// </summary>
    [Fact]
    public void Meal_entry_handoff_presents_editor_before_closing_chooser()
    {
        var pageSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "MuscleCuties.UI", "Pages", "Nutrition", "NutritionPage.xaml.cs"));

        var loadEditor = pageSource.IndexOf(
            "MealEditor.LoadIfNeededAsync", StringComparison.Ordinal);
        var presentEditor = pageSource.IndexOf(
            "editorPresented = editorModal.CompleteOpenTransition()", StringComparison.Ordinal);
        var closeDecision = pageSource.IndexOf(
            "NutritionMealEntryHandoff.CanCloseChooser", StringComparison.Ordinal);
        var closeChooser = pageSource.IndexOf(
            "CloseMealEntryCommand.Execute", StringComparison.Ordinal);

        Assert.True(loadEditor >= 0, "NutritionPage must load the editor during a handoff.");
        Assert.True(presentEditor > loadEditor,
            "The editor must be presented after it is loaded and before the chooser closes.");
        Assert.True(closeDecision > presentEditor,
            "The close decision must be made only after the editor reports a completed presentation.");
        Assert.True(closeChooser > closeDecision,
            "The chooser must not be dismissed before the handoff policy permits it.");
        Assert.Contains("NutritionMealEntryHandoff.IsHandoff", pageSource);
    }

    /// <summary>
    /// MauiProgram is the only place that hands a route to Shell. The diary consumes its intent
    /// through IQueryAttributable, so the query must survive this boundary; only the login-root
    /// comparison is allowed to use the query-free form.
    /// </summary>
    [Fact]
    public void Shell_navigation_preserves_query_intent_for_the_food_log()
    {
        var root = RepositoryRoot();
        var programSource = File.ReadAllText(Path.Combine(root, "src", "MuscleCuties.UI", "MauiProgram.cs"));
        var foodLogSource = File.ReadAllText(Path.Combine(
            root, "src", "MuscleCuties.UI", "Pages", "Nutrition", "FoodLogPage.xaml.cs"));

        Assert.Contains(
            "await shell.GoToAsync(ShellNavigationRoute.ForShell(route), false);",
            programSource);
        Assert.Contains("ShellNavigationRoute.IsLoginRoot(route)", programSource);
        Assert.DoesNotContain("GoToAsync(ShellNavigationRoute.WithoutQuery", programSource);
        Assert.DoesNotContain("RemoveQueryString", programSource);

        Assert.Contains("IQueryAttributable", foodLogSource);
        Assert.Contains("NutritionNavigation.TryReadEntryId(query", foodLogSource);
        Assert.Contains("NutritionNavigation.TryReadAddFoodIntent(query", foodLogSource);
    }

    private static string? AppliedStyleName(XElement element)
    {
        var reference = element.Attribute("Style")?.Value;
        if (reference is not null)
        {
            var match = StaticResourceReference.Match(reference);
            if (match.Success)
                return match.Groups["key"].Value;
        }

        return element.Name.LocalName == "Label" ? ImplicitLabelStyle : null;
    }

    private static Dictionary<string, (string? BasedOn, string? Light, string? Dark)> ReadLabelTextColours(string path)
    {
        var styles = new Dictionary<string, (string? BasedOn, string? Light, string? Dark)>(StringComparer.Ordinal);

        foreach (var style in XDocument.Load(path).Descendants().Where(element => element.Name.LocalName == "Style"))
        {
            var key = style.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value;
            var targetType = style.Attribute("TargetType")?.Value;
            if (key is null && !string.Equals(targetType, "Label", StringComparison.Ordinal))
                continue;

            string? basedOn = null;
            var basedOnReference = style.Attribute("BasedOn")?.Value;
            if (basedOnReference is not null)
            {
                var basedOnMatch = StaticResourceReference.Match(basedOnReference);
                if (basedOnMatch.Success)
                    basedOn = basedOnMatch.Groups["key"].Value;
            }

            var colourValue = style.Elements()
                .Where(element => element.Name.LocalName == "Setter" &&
                                  string.Equals(element.Attribute("Property")?.Value, "TextColor", StringComparison.Ordinal))
                .Select(element => element.Attribute("Value")?.Value)
                .FirstOrDefault(value => value is not null);

            if (colourValue is not null && ThemeColourPair.Match(colourValue) is { Success: true } pair)
                styles[key ?? ImplicitLabelStyle] = (basedOn, pair.Groups["light"].Value, pair.Groups["dark"].Value);
            else
                styles[key ?? ImplicitLabelStyle] = (basedOn, null, null);
        }

        return styles;
    }

    private static (string Light, string Dark)? ResolveTextColour(
        Dictionary<string, (string? BasedOn, string? Light, string? Dark)> styles, string key)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (visited.Add(key) && styles.TryGetValue(key, out var style))
        {
            if (style.Light is not null && style.Dark is not null)
                return (style.Light, style.Dark);
            if (style.BasedOn is null)
                return null;
            key = style.BasedOn;
        }

        return null;
    }

    private static IEnumerable<string> SourceFiles(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Relative(string path, string root) => Path.GetRelativePath(root, path);

    private static string RepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "MuscleCuties.sln")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }
}
