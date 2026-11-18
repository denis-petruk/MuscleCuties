using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Source-level guards for the single localization catalog. These tests deliberately read the UI
/// sources as text instead of referencing the MAUI project, so they run in the backend gate.
/// </summary>
public class LocalizationCatalogTests
{
    private static readonly string RepoRoot = FindRepositoryRoot();
    private static readonly string UiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
    private static readonly string LocalizationDirectory = Path.Combine(UiRoot, "Services", "Localization");

    private static readonly Regex XamlKeyRegex = new(
        @"loc:LocalizedText\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);

    private static readonly Regex CodeKeyRegex = new(
        @"UiTextCatalog\.(?:Get|Format)\(\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex RuntimeTextLiteralRegex = new(
        @"TranslateRuntimeText\(\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex HashKeyRegex = new(
        @"\[""T[0-9A-F]{10}""\]", RegexOptions.Compiled);

    private static readonly Regex FormatArgumentRegex = new(
        @"\{\d+(?::[^}]*)?\}", RegexOptions.Compiled);

    private static readonly Regex CatalogEntryRegex = new(
        @"\[""(?<key>[^""]+)""\]\s*=\s*(?<value>""(?:[^""\\]|\\.)*"")",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RuntimeMapEntryRegex = new(
        @"^\s*\[""(?<key>(?:[^""\\]|\\.)*)""\]\s*=",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void Every_xaml_localized_key_exists_in_the_catalog()
    {
        var catalog = ReadEnglishCatalog();
        var missing = new List<string>();

        foreach (var file in EnumerateUiFiles("*.xaml"))
        {
            foreach (Match match in XamlKeyRegex.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[1].Value;
                if (!catalog.ContainsKey(key))
                    missing.Add($"{Path.GetRelativePath(RepoRoot, file)}: {key}");
            }
        }

        Assert.True(
            missing.Count == 0,
            $"Found {missing.Count} XAML localization key(s) missing from the catalog:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void Every_code_key_literal_exists_in_the_catalog()
    {
        var catalog = ReadEnglishCatalog();
        var missing = new List<string>();

        foreach (var file in EnumerateUiFiles("*.cs"))
        {
            foreach (Match match in CodeKeyRegex.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[1].Value;
                if (!catalog.ContainsKey(key))
                    missing.Add($"{Path.GetRelativePath(RepoRoot, file)}: {key}");
            }
        }

        Assert.True(
            missing.Count == 0,
            $"Found {missing.Count} UiTextCatalog.Get/Format key literal(s) missing from the catalog:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void English_catalog_contains_no_cyrillic_text()
    {
        var violations = ReadEnglishCatalog()
            .Where(pair => Regex.IsMatch(pair.Value, @"[\u0400-\u04FF]"))
            .Select(pair => pair.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"English catalog values contain Cyrillic text: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Russian_copy_contains_no_mixed_script_words()
    {
        var violations = ReadRussianCopy()
            .Where(pair => Regex.IsMatch(pair.Value, @"(?:[А-Яа-яЁё][A-Za-z]|[A-Za-z][А-Яа-яЁё])"))
            .Select(pair => $"{pair.Key}: {pair.Value}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Russian copy contains mixed-script words:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void Russian_copy_does_not_leak_common_english_ui_words()
    {
        string[] forbidden =
        [
            "logo", "Activities", "baseline", "recovery", "Consistently", "feels", "brutal",
            "lookup", "timed out", "Nutrition", "Session", "check-in", "Photo Journey",
            "email", "example.com", "FoodData Central", "1RM", "RelayCommand", "ViewModel", "XAML"
        ];
        var violations = ReadRussianCopy()
            .Where(pair => forbidden.Any(word => Regex.IsMatch(
                pair.Value, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase)))
            .Select(pair => $"{pair.Key}: {pair.Value}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Russian copy contains English UI words:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void English_and_russian_catalogs_have_the_same_keys()
    {
        var english = ReadEnglishCatalog();
        var russian = ReadRussianCatalog();

        var missingInRussian = english.Keys.Except(russian.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var missingInEnglish = russian.Keys.Except(english.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingInRussian.Length == 0 && missingInEnglish.Length == 0,
            $"Catalog key sets differ. Missing in Russian: {string.Join(", ", missingInRussian)}. " +
            $"Missing in English: {string.Join(", ", missingInEnglish)}.");
    }

    [Fact]
    public void English_and_russian_format_arguments_match()
    {
        var english = ReadEnglishCatalog();
        var russian = ReadRussianCatalog();
        var mismatches = new List<string>();

        foreach (var (key, englishValue) in english)
        {
            if (!russian.TryGetValue(key, out var russianValue))
                continue;

            var englishArguments = FormatArguments(englishValue);
            var russianArguments = FormatArguments(russianValue);
            if (!englishArguments.SequenceEqual(russianArguments, StringComparer.Ordinal))
            {
                mismatches.Add(
                    $"{key}: EN [{string.Join(", ", englishArguments)}] / RU [{string.Join(", ", russianArguments)}]");
            }
        }

        Assert.True(
            mismatches.Count == 0,
            $"Found {mismatches.Count} key(s) with mismatched format arguments:\n" +
            string.Join("\n", mismatches));
    }

    [Fact]
    public void Catalog_contains_no_hash_keys()
    {
        var matches = new List<string>();

        foreach (var file in Directory.GetFiles(LocalizationDirectory, "*.cs"))
        {
            foreach (Match match in HashKeyRegex.Matches(File.ReadAllText(file)))
                matches.Add($"{Path.GetFileName(file)}: {match.Value}");
        }

        Assert.True(
            matches.Count == 0,
            $"Found {matches.Count} retired hash key(s) in the localization sources:\n" +
            string.Join("\n", matches));
    }

    [Fact]
    public void Runtime_text_adapter_is_never_given_a_key_literal()
    {
        var catalog = ReadEnglishCatalog();
        var violations = new List<string>();

        foreach (var file in EnumerateUiFiles("*.cs"))
        {
            foreach (Match match in RuntimeTextLiteralRegex.Matches(File.ReadAllText(file)))
            {
                var literal = match.Groups[1].Value;
                if (catalog.ContainsKey(literal))
                    violations.Add($"{Path.GetRelativePath(RepoRoot, file)}: {literal}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"TranslateRuntimeText received catalog key literal(s):\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void Russian_nutrition_focus_heading_is_distinct_from_the_page_title()
    {
        var source = File.ReadAllText(Path.Combine(LocalizationDirectory, "UiTextCatalog.RuntimeText.cs"));
        Assert.Contains("[\"Nutrition focus\"] = \"Фокус питания\",", source, StringComparison.Ordinal);
        Assert.Contains("[\"Nutrition\"] = \"Питание\",", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_map_has_no_duplicate_english_values()
    {
        var runtimePath = Path.Combine(LocalizationDirectory, "UiTextCatalog.RuntimeText.cs");
        Assert.True(File.Exists(runtimePath), $"Runtime catalog not found at {runtimePath}");

        var entries = RuntimeMapEntryRegex.Matches(File.ReadAllText(runtimePath))
            .Select(match => match.Groups["key"].Value)
            .ToArray();

        Assert.NotEmpty(entries);
        var duplicates = entries.GroupBy(key => key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"Runtime map contains duplicate English values: {string.Join(", ", duplicates)}");
    }

    [Theory]
    [InlineData("Workout_Generation_MissingUpperPullEquipment")]
    [InlineData("Workout_Generation_MissingUpperPullBandAnchor")]
    [InlineData("Workout_Generation_MissingDirectCalves")]
    [InlineData("Workout_Generation_MissingDirectRearDelts")]
    [InlineData("Workout_Generation_BelowTargetDynamicCore")]
    [InlineData("Workout_Generation_MissingGluteFocusAbduction")]
    [InlineData("Workout_Generation_MissingKneeExtension")]
    [InlineData("Workout_Generation_MissingKneeFlexion")]
    [InlineData("Workout_Generation_MissingHipExtension")]
    [InlineData("Workout_Generation_MovementCoverageUnknown")]
    [InlineData("Workout_Generation_ReducedVolumeNotice")]
    [InlineData("Workout_Generation_OneDayCoverageNotice")]
    [InlineData("Workout_Generation_CoverageGapNotice")]
    [InlineData("Workout_Generation_CoveragePreferredNotice")]
    public void Preview_coverage_notes_stay_concise(string key)
    {
        const int maximumLength = 60;
        var russian = ReadRussianCopy();

        Assert.True(russian.TryGetValue(key, out var text), $"Missing Russian copy for {key}");
        Assert.True(text.Length <= maximumLength,
            $"{key} is {text.Length} characters long: {text}");
    }

    [Fact]
    public void Every_reviewed_exercise_name_has_russian_copy()
    {
        var catalogPath = Path.Combine(
            RepoRoot, "src", "MuscleCuties.Infrastructure.PostgreSql", "Workout", "Planning",
            "ReviewData", "ReviewedExerciseCatalogV020.json");
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var names = new HashSet<string>(StringComparer.Ordinal);
        CollectExpectedExerciseNames(document.RootElement, names);

        var seededPath = Path.Combine(LocalizationDirectory, "SeededCopyRussianText.cs");
        var seeded = CatalogEntryRegex.Matches(File.ReadAllText(seededPath))
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["value"].Value,
                StringComparer.Ordinal);

        var missing = names.Where(name => !seeded.ContainsKey(name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.True(
            missing.Length == 0,
            $"Reviewed exercise names without Russian seeded copy:\n{string.Join("\n", missing)}");
    }

    [Fact]
    public void Localization_service_owns_no_string_catalog()
    {
        var service = File.ReadAllText(Path.Combine(LocalizationDirectory, "LocalizationService.cs"));
        var contract = File.ReadAllText(Path.Combine(LocalizationDirectory, "ILocalizationService.cs"));

        string[] retiredMembers = ["RuStrings", "EnStrings", "GetString", "this["];

        foreach (var member in retiredMembers)
        {
            Assert.DoesNotContain(member, service, StringComparison.Ordinal);
            Assert.DoesNotContain(member, contract, StringComparison.Ordinal);
        }
    }

    private static void CollectExpectedExerciseNames(JsonElement element, HashSet<string> names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("expectedName") && property.Value.ValueKind == JsonValueKind.String)
                    names.Add(property.Value.GetString()!);
                else
                    CollectExpectedExerciseNames(property.Value, names);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectExpectedExerciseNames(item, names);
        }
    }

    private static Dictionary<string, string> ReadEnglishCatalog()
    {
        return ReadCatalog("UiTextCatalog", excludedFileName: "UiTextCatalog.RuntimeText.cs");
    }

    private static Dictionary<string, string> ReadRussianCatalog()
    {
        return ReadCatalog("RussianUiText", excludedFileName: null);
    }

    private static Dictionary<string, string> ReadRussianCopy()
    {
        var copy = ReadRussianCatalog();
        var runtimePath = Path.Combine(LocalizationDirectory, "UiTextCatalog.RuntimeText.cs");
        foreach (Match match in CatalogEntryRegex.Matches(File.ReadAllText(runtimePath)))
            copy[match.Groups["key"].Value] = match.Groups["value"].Value;

        foreach (var file in Directory.GetFiles(LocalizationDirectory, "SeededCopyRussianText.cs"))
        {
            foreach (Match match in CatalogEntryRegex.Matches(File.ReadAllText(file)))
                copy[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        return copy;
    }

    private static Dictionary<string, string> ReadCatalog(string filePrefix, string? excludedFileName)
    {
        var catalog = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in Directory.GetFiles(LocalizationDirectory, $"{filePrefix}*.cs"))
        {
            if (excludedFileName is not null &&
                string.Equals(Path.GetFileName(file), excludedFileName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in CatalogEntryRegex.Matches(File.ReadAllText(file)))
                catalog[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        return catalog;
    }

    private static string[] FormatArguments(string value)
    {
        return FormatArgumentRegex.Matches(value)
            .Select(match => match.Value)
            .OrderBy(argument => argument, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateUiFiles(string pattern)
    {
        return Directory.EnumerateFiles(UiRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
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

        throw new InvalidOperationException($"Could not locate repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
