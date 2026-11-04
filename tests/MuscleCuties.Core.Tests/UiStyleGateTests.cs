using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Deterministic style gate for the parts of the visual contract a renderer is not needed to
/// verify: token-only consumer values, one definition per shared key, one voice per action, and
/// contrast between the dictionary roles the app actually composes.
/// </summary>
public sealed class UiStyleGateTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string UiRoot = Path.Combine(RepositoryRoot, "src", "MuscleCuties.UI");
    private static readonly string ColorsPath = Path.Combine(UiRoot, "Resources", "Styles", "Colors.xaml");
    private static readonly string StylesPath = Path.Combine(UiRoot, "Resources", "Styles", "Styles.xaml");
    private static readonly string LocalizationRoot = Path.Combine(UiRoot, "Services", "Localization");

    private static readonly Regex RawStyleValue = new(
        @"\b(?<property>Padding|Margin|Spacing|RowSpacing|ColumnSpacing|CornerRadius|StrokeThickness|FontSize|CharacterSpacing|WidthRequest|HeightRequest|MinimumHeightRequest|MinimumWidthRequest|MaximumWidthRequest|MaximumHeightRequest)=""(?<value>[0-9]+(?:\.[0-9]+)?(?:,[0-9]+(?:\.[0-9]+)?)*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralTypography = new(
        @"\b(?<property>FontFamily|FontAttributes)=""(?<value>[^""{][^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ColorLiteral = new(
        @"#[0-9A-Fa-f]{6,8}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralThickness = new(
        @"new\s+Thickness\(\s*[0-9]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CSharpResourceLookup = new(
        @"(?:Resources\s*\[\s*|AppThemeResources\.Get(?:Required|Color|Double|Thickness)\s*\(\s*)""(?<key>[A-Za-z0-9_]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CatalogAssignment = new(
        @"\b(?:English|Values)\s*\[\s*""(?<key>[^""]+)""\s*\]\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EnglishValue = new(
        @"\bEnglish\s*\[\s*""[^""]+""\s*\]\s*=\s*""(?<value>(?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ObsoletePickerPrefixes =
    [
        "Nutrition_MealSuggestion_ChooseAMeal",
        "Nutrition_MealSuggestion_ChooseMeal"
    ];

    // Copy that names a control or input gesture the current screens do not present.
    private static readonly string[] ForbiddenCopyPhrases =
    [
        "choose a meal",
        "choose meal",
        "header icon",
        "food log icon",
        "camera icon",
        "click"
    ];

    private static readonly (string Foreground, string Background)[] NormalTextPairs =
    [
        ("TextPrimary", "PageBackground"),
        ("TextSecondary", "PageBackground"),
        ("TextSecondary", "CardSurface"),
        ("TextSecondary", "CardSurfaceSecondary"),
        ("TextTertiaryLight", "PageBackground"),
        ("TextTertiaryLight", "CardSurfaceSecondary"),
        ("TextTertiaryLight", "AccentSurface"),
        ("ActionPrimaryLight", "CardSurfaceSecondary"),
        ("ActionSecondaryLight", "CardSurfaceSecondary"),
        ("SuccessLight", "CardSurfaceSecondary"),
        ("WarningAccentLight", "CardSurfaceSecondary"),
        ("ErrorLight", "CardSurfaceSecondary"),
        ("TextPrimaryDark", "PageBackgroundDark"),
        ("TextSecondaryDark", "PageBackgroundDark"),
        ("TextSecondaryDark", "CardSurfaceSecondaryDark"),
        ("TextTertiaryDark", "PageBackgroundDark"),
        ("TextTertiaryDark", "CardSurfaceDark"),
        ("TextTertiaryDark", "CardSurfaceSecondaryDark"),
        ("SuccessDark", "CardSurfaceSecondaryDark"),
        ("ErrorDark", "CardSurfaceSecondaryDark"),
        ("TextPrimary", "ActionSurfaceDark"),
        ("ContrastInk", "CycleNeutralTodaySurface")
    ];

    [Fact]
    public void Consumer_style_values_resolve_through_shared_tokens()
    {
        var violations = new List<string>();

        foreach (var file in SourceFiles(UiRoot, "*.xaml"))
        {
            var relative = Relative(file);
            if (relative.Contains("/Resources/", StringComparison.Ordinal))
                continue;

            var text = File.ReadAllText(file);
            foreach (Match match in RawStyleValue.Matches(text))
            {
                if (match.Groups["value"].Value == "0")
                    continue;

                violations.Add(
                    $"{relative}: {match.Value} is a raw style value; use a shared Ui* token.");
            }

            foreach (Match match in LiteralTypography.Matches(text))
            {
                violations.Add(
                    $"{relative}: {match.Value} sets typography inline; use a shared Label style.");
            }

            foreach (Match match in ColorLiteral.Matches(text))
            {
                violations.Add(
                    $"{relative}: {match.Value} is a raw colour literal; use a semantic role.");
            }
        }

        foreach (var file in SourceFiles(UiRoot, "*.cs"))
        {
            var relative = Relative(file);
            if (relative == "Resources/Styles/Colors.xaml" || relative.Equals("MauiProgram.cs", StringComparison.Ordinal))
                continue;

            foreach (Match match in LiteralThickness.Matches(File.ReadAllText(file)))
            {
                violations.Add(
                    $"{relative}: {match.Value} creates a raw Thickness; resolve a shared token.");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"{violations.Count} consumer style value(s) bypass the shared dictionaries:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Shared_resource_and_style_keys_have_exactly_one_definition()
    {
        var violations = new List<string>();
        var keySites = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in new[] { ColorsPath, StylesPath, Path.Combine(UiRoot, "App.xaml") })
        {
            var relative = Relative(file);
            foreach (var element in XDocument.Load(file).Descendants())
            {
                var key = element.Attributes()
                    .FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value;
                if (key is null)
                    continue;

                if (!keySites.TryAdd(key, relative))
                    violations.Add($"'{key}' is defined in both {keySites[key]} and {relative}.");
            }
        }

        var styles = XDocument.Load(StylesPath).Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Where(element => element.Attribute("TargetType") is not null)
            .ToArray();

        foreach (var style in styles)
        {
            var key = style.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value ?? "implicit";
            var duplicateSetters = style.Elements()
                .Where(element => element.Name.LocalName == "Setter")
                .Select(element => element.Attribute("Property")?.Value)
                .Where(property => property is not null)
                .GroupBy(property => property!, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicateSetters.Length > 0)
            {
                violations.Add(
                    $"Style '{key}' sets {string.Join(", ", duplicateSetters)} more than once.");
            }
        }

        var duplicateStyles = styles
            .Where(style => style.Attributes().Any(attribute => attribute.Name.LocalName == "Key"))
            .GroupBy(StyleSignature, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(StyleKey)))
            .ToArray();

        foreach (var duplicates in duplicateStyles)
            violations.Add($"Styles are semantically identical: {duplicates}.");

        var uiSource = string.Join("\n",
            SourceFiles(UiRoot, "*.xaml").Concat(SourceFiles(UiRoot, "*.cs"))
                .Select(File.ReadAllText));
        foreach (var style in styles.Where(style => style.Attributes().Any(attribute => attribute.Name.LocalName == "Key")))
        {
            var key = StyleKey(style);
            var reference = Regex.IsMatch(uiSource,
                "StaticResource\\s+" + Regex.Escape(key) +
                "\\b|Resources\\s*\\[\\s*\"" + Regex.Escape(key) + "\"\\s*\\]");
            if (!reference)
                violations.Add($"Shared style '{key}' has no consumer and is dead code.");
        }

        var xamlKeys = SourceFiles(UiRoot, "*.xaml")
            .SelectMany(file => XDocument.Load(file).Descendants())
            .Select(element => element.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in SourceFiles(UiRoot, "*.cs"))
        {
            foreach (Match match in CSharpResourceLookup.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups["key"].Value;
                if (!xamlKeys.Contains(key))
                {
                    violations.Add(
                        $"{Relative(file)} resolves missing resource '{key}'; the key has no XAML definition.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"{violations.Count} duplicated or contradictory style definition(s):\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Catalog_copy_is_assigned_once_and_names_only_current_actions()
    {
        var violations = new List<string>();
        var catalogFiles = Directory.EnumerateFiles(LocalizationRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file => Path.GetFileName(file).StartsWith("UiTextCatalog", StringComparison.Ordinal) ||
                           Path.GetFileName(file).StartsWith("RussianUiText", StringComparison.Ordinal))
            .ToArray();

        foreach (var file in catalogFiles)
        {
            var assignments = CatalogAssignment.Matches(File.ReadAllText(file))
                .Select(match => match.Groups["key"].Value)
                .ToArray();

            foreach (var duplicate in assignments
                         .GroupBy(key => key, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                violations.Add(
                    $"{Relative(file)} assigns '{duplicate.Key}' {duplicate.Count()} times; the last value silently wins.");
            }

            foreach (var forbidden in ForbiddenCopyPhrases)
            {
                if (File.ReadAllText(file).Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{Relative(file)} contains obsolete copy '{forbidden}'.");
            }

            foreach (var obsoleteKey in ObsoletePickerPrefixes)
            {
                if (assignments.Contains(obsoleteKey, StringComparer.Ordinal))
                    violations.Add(
                        $"{Relative(file)} still defines the removed meal-picker key '{obsoleteKey}'.");
            }
        }

        var englishValues = catalogFiles
            .SelectMany(file => EnglishValue.Matches(File.ReadAllText(file)))
            .Select(match => match.Groups["value"].Value)
            .ToArray();
        foreach (var forbidden in ForbiddenCopyPhrases)
        {
            if (englishValues.Any(value => value.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
                violations.Add($"English copy still describes a removed action: '{forbidden}'.");
        }

        Assert.True(
            violations.Count == 0,
            $"{violations.Count} catalog definition or copy mismatch(es):\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Dictionary_contrast_pairs_meet_the_wcag_floor()
    {
        var colors = Regex.Matches(File.ReadAllText(ColorsPath),
                "<Color x:Key=\"(?<key>[^\"]+)\">(?<value>#[0-9A-Fa-f]{6})</Color>")
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["value"].Value,
                StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var (foreground, background) in NormalTextPairs)
        {
            Assert.True(colors.ContainsKey(foreground), $"Missing foreground role '{foreground}'.");
            Assert.True(colors.ContainsKey(background), $"Missing background role '{background}'.");

            var ratio = ContrastRatio(colors[foreground], colors[background]);
            if (ratio < 4.5)
            {
                failures.Add(
                    $"{foreground} on {background} measures {ratio.ToString("F2", CultureInfo.InvariantCulture)}:1, below 4.5:1.");
            }
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} declared text pair(s) miss the normal-text contrast floor:\n"
            + string.Join("\n", failures));
    }

    private static string StyleSignature(XElement style)
    {
        var setters = style.Elements()
            .Where(element => element.Name.LocalName == "Setter")
            .Select(element =>
                $"{element.Attribute("Property")?.Value}={element.Attribute("Value")?.Value}")
            .OrderBy(value => value, StringComparer.Ordinal);

        return string.Join("\u001f", new[]
        {
            style.Attribute("TargetType")?.Value ?? string.Empty,
            style.Attribute("BasedOn")?.Value ?? string.Empty,
            string.Join("\u001e", setters)
        });
    }

    private static string StyleKey(XElement style) =>
        style.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value ?? "implicit";

    private static IEnumerable<string> SourceFiles(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

    private static double ContrastRatio(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var value = hex.TrimStart('#');
        return 0.2126 * LinearChannel(value, 0)
               + 0.7152 * LinearChannel(value, 2)
               + 0.0722 * LinearChannel(value, 4);
    }

    private static double LinearChannel(string hex, int offset)
    {
        var channel = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255d;
        return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static string FindRepositoryRoot()
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

        throw new InvalidOperationException(
            $"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }
}
