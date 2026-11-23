using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Infrastructure.Caching;
using MuscleCuties.Infrastructure.Cloud;
using MuscleCuties.Infrastructure.Services.Media;
using MuscleCuties.UI.Services.Localization;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// The pass-6 merges each install one owner for behaviour that used to exist twice. These assertions are
/// written against the owner, so a second private copy reappearing is a behavioural change, not a refactor.
/// </summary>
public sealed class Pass6SharedOwnersTests
{
    [Theory]
    [InlineData("FOUNDATION", 0)]
    [InlineData("foundation", 0)]
    [InlineData("SR LEGACY", 1)]
    [InlineData("SURVEY (FNDDS)", 2)]
    [InlineData("BRANDED", 3)]
    [InlineData("OTHER", 4)]
    [InlineData(null, 4)]
    public void Usda_data_type_rank_is_owned_by_the_ranking_rule(string? dataType, int expected)
    {
        Assert.Equal(expected, FoodCatalogRankingRule.DataTypeRank(dataType));
    }

    [Fact]
    public void Searchable_text_is_name_plus_brand_identifiers_then_ingredients()
    {
        var primary = FoodCatalogRankingRule.BuildPrimarySearchableText("Oats", "Oats Co", "Owner", "0123");

        Assert.Equal("Oats Oats Co Owner 0123", primary);
        Assert.Equal("Oats Oats Co Owner 0123 Rolled oats",
            FoodCatalogRankingRule.BuildFullSearchableText(primary, "Rolled oats"));
    }

    [Theory]
    [InlineData("UTC", true)]
    [InlineData("Europe/Kyiv", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Not/AZone", false)]
    public void Time_zone_validity_is_owned_by_the_check_in_rule(string? timeZoneId, bool expected)
    {
        Assert.Equal(expected, CheckInObservationRule.IsValidTimeZoneId(timeZoneId));
    }

    [Theory]
    [InlineData("[3,7]", new[] { 3, 7 })]
    [InlineData("[3,0,-1]", new[] { 3 })]
    [InlineData("[]", new int[0])]
    [InlineData("", new int[0])]
    [InlineData(null, new int[0])]
    [InlineData("{\"ids\":[1]}", new int[0])]
    [InlineData("not json", new int[0])]
    public void Excluded_exercise_ids_parse_to_positive_ints_or_nothing(string? json, int[] expected)
    {
        Assert.Equal(expected, ExcludedExerciseIds.Parse(json).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void Stable_int_id_is_positive_and_stable_for_the_same_guid()
    {
        var id = Guid.NewGuid();

        var first = StableIntId.From(id);

        Assert.True(first > 0);
        Assert.Equal(first, StableIntId.From(id));
        Assert.True(StableIntId.From(Guid.NewGuid()) > 0);
    }

    [Fact]
    public void Media_path_is_inside_root_only_when_fully_qualified_and_below_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "mc-root");
        var inside = Path.Combine(root, "photo.jpg");
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "photo.jpg");

        Assert.True(MediaPathSafety.IsWithinRoot(inside, root));
        Assert.False(MediaPathSafety.IsWithinRoot(outside, root));
        Assert.False(MediaPathSafety.IsWithinRoot("photo.jpg", root));
        Assert.False(MediaPathSafety.IsWithinRoot(inside, null));
    }

    [Fact]
    public async Task Account_scoped_cache_clear_drops_the_account_before_clearing_and_releases_the_gate()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var steps = new List<string>();

        await AccountScopedCacheClear.RunAsync(gate, () => steps.Add("reset"),
            _ => { steps.Add("clear"); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(["reset", "clear"], steps);
        Assert.Equal(1, gate.CurrentCount);
    }

    [Theory]
    [InlineData("Running", "run", true)]
    [InlineData("Tempo run", "run", true)]
    [InlineData("Crunch", "run", false)]
    [InlineData("Plank", "plank", true)]
    public void Activity_term_matching_uses_word_stems(string name, string term, bool expected)
    {
        Assert.Equal(expected, WorkoutActivityTagRule.ContainsAny(name, term));
    }

    [Fact]
    public void Localized_format_has_one_definition_and_no_private_copy_at_the_callers()
    {
        // The catalogs are not linked into this assembly, so the substitution itself is asserted through a
        // source guard: exactly one implementation, and both previously-duplicated callers use it.
        var root = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(root) && !File.Exists(Path.Combine(root, "MuscleCuties.sln")))
            root = Directory.GetParent(root)?.FullName;
        Assert.False(string.IsNullOrEmpty(root));

        var mapper = File.ReadAllText(Path.Combine(root!, "src", "MuscleCuties.UI", "ViewModels", "Workout",
            "StrengthPlanGenerationDisplayMapper.cs"));
        var display = File.ReadAllText(Path.Combine(root!, "src", "MuscleCuties.UI", "ViewModels", "Workout",
            "StrengthPlanSessionDisplay.cs"));
        var owner = File.ReadAllText(Path.Combine(root!, "src", "MuscleCuties.UI", "Services", "Localization",
            "LocalizedFormat.cs"));

        Assert.Contains("public static string Format(string key, params string[] arguments)", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("static string FormatLocalized(", mapper, StringComparison.Ordinal);
        Assert.DoesNotContain("static string FormatLocalized(", display, StringComparison.Ordinal);
        Assert.Contains("LocalizedFormat.Format(", mapper, StringComparison.Ordinal);
        Assert.Contains("LocalizedFormat.Format(", display, StringComparison.Ordinal);
    }
}
