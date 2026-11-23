using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// One localization key has exactly one English and one Russian definition. The catalogs are built by
/// partial methods that write into dictionaries, so a duplicated key silently wins by assignment order
/// instead of failing compilation. This scan makes that collision a build failure.
/// </summary>
public sealed class LocalizationCatalogUniquenessTests
{
    private static readonly Regex EnglishAssignment = new(
        "English\\[\"(?<key>[^\"]+)\"\\]\\s*=", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RussianAssignment = new(
        "Values\\[\"(?<key>[^\"]+)\"\\]\\s*=", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void No_key_is_assigned_twice_in_the_same_language()
    {
        var root = RepositoryRoot();
        var catalogDirectory = Path.Combine(root, "src", "MuscleCuties.UI", "Services", "Localization");
        var englishDirectory = catalogDirectory;
        var collisions = new List<string>();

        foreach (var (pattern, language, files) in new[]
                 {
                     (EnglishAssignment, "English", UiTextFiles(catalogDirectory)),
                     (RussianAssignment, "Russian", RussianFiles(englishDirectory))
                 })
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                foreach (Match match in pattern.Matches(File.ReadAllText(file)))
                {
                    var key = match.Groups["key"].Value;
                    if (seen.TryGetValue(key, out var first))
                    {
                        collisions.Add($"{language} '{key}' in {Path.GetFileName(file)} and {Path.GetFileName(first)}.");
                    }
                    else
                    {
                        seen[key] = file;
                    }
                }
            }
        }

        Assert.True(collisions.Count == 0, string.Join(Environment.NewLine, collisions));
    }

    private static IEnumerable<string> UiTextFiles(string directory) =>
        Directory.EnumerateFiles(directory, "UiTextCatalog*.cs", SearchOption.TopDirectoryOnly);

    private static IEnumerable<string> RussianFiles(string directory) =>
        Directory.EnumerateFiles(directory, "RussianUiText*.cs", SearchOption.TopDirectoryOnly);

    private static string RepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "MuscleCuties.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }
}
