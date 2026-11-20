using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Server-seeded exercise and quiz copy reaches the UI as English literals and is translated by the
/// runtime map. This guard fails when a seeded name or question has no Russian entry, which is how
/// an English exercise name used to survive a Russian session.
/// </summary>
public class RussianRuntimeCopyCoverageTests
{
    private static readonly string RepoRoot = FindRepositoryRoot();
    private static readonly string RuntimeMapPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Services",
        "Localization", "UiTextCatalog.RuntimeText.cs");
    private static readonly string ManifestPath = Path.Combine(RepoRoot, "src",
        "MuscleCuties.Infrastructure.PostgreSql", "Persistence", "Seed", "CanonicalCatalogManifest.json");
    private static readonly string SeededCopyPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Services",
        "Localization", "SeededCopyRussianText.cs");

    private static readonly Regex RuntimeKeyRegex = new(
        @"^\s*\[""(?<key>(?:[^""\\]|\\.)*)""\]\s*=", RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void Every_seeded_exercise_name_has_a_russian_translation()
    {
        var missing = SeededExerciseNames().Where(name => !RuntimeKeys().Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            $"Seeded exercise name(s) without a Russian runtime translation ({missing.Length}):\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void Every_seeded_quiz_question_and_answer_has_a_russian_translation()
    {
        var keys = RuntimeKeys();
        var missing = SeededQuizText().Where(text => !keys.Contains(text))
            .OrderBy(text => text, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            $"Quiz question/answer text without a Russian runtime translation ({missing.Length}):\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void Workout_section_titles_and_phase_pair_headings_have_a_russian_translation()
    {
        string[] required =
        [
            "Strength & Sculpt",
            "Cardio & Conditioning",
            "Mobility & Recovery",
            "Rest & Recovery",
            "How does your period usually feel?",
            "How does your follicular phase usually feel?",
            "How does ovulation usually feel?",
            "How does your luteal phase usually feel?"
        ];

        var keys = RuntimeKeys();
        var missing = required.Where(text => !keys.Contains(text)).ToArray();

        Assert.True(missing.Length == 0,
            $"Workout copy without a Russian runtime translation: {string.Join(", ", missing)}");
    }

    private static HashSet<string> RuntimeKeys() =>
        new[] { RuntimeMapPath, SeededCopyPath }
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"^\s*\[""(?<key>(?:[^""\\]|\\.)*)""\]\s*=",
                RegexOptions.Multiline))
            .Select(match => match.Groups["key"].Value.Replace("\\\"", "\"", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> SeededExerciseNames() => SeedRows("Exercise")
        .Select(row => row.TryGetProperty("sourceName", out var name) ? name.GetString() : null)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name!)
        .Distinct(StringComparer.Ordinal);

    private static IEnumerable<string> SeededQuizText()
    {
        foreach (var row in SeedRows("QuizQuestion"))
        {
            if (row.TryGetProperty("sourceQuestion", out var question) &&
                !string.IsNullOrWhiteSpace(question.GetString()))
                yield return question.GetString()!;
        }

        foreach (var row in SeedRows("QuizAnswer"))
        {
            if (row.TryGetProperty("sourceText", out var answer) &&
                !string.IsNullOrWhiteSpace(answer.GetString()))
                yield return answer.GetString()!;
        }
    }

    private static IEnumerable<JsonElement> SeedRows(string entityType)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var rows = document.RootElement.GetProperty("entityTypes").EnumerateArray()
            .Single(entity => entity.GetProperty("entityType").GetString() == entityType)
            .GetProperty("rows").EnumerateArray().ToArray();
        // The document is disposed after the array is materialised; clone so callers can read it.
        return rows.Select(row => row.Clone()).ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MuscleCuties.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
