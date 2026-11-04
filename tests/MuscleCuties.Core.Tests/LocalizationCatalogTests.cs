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

    private static Dictionary<string, string> ReadEnglishCatalog()
    {
        return ReadCatalog("UiTextCatalog", excludedFileName: "UiTextCatalog.RuntimeText.cs");
    }

    private static Dictionary<string, string> ReadRussianCatalog()
    {
        return ReadCatalog("RussianUiText", excludedFileName: null);
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
