using System.Text.RegularExpressions;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Programmatic verification of UI Cleanliness, Control Usage, and Route Reachability (UIC01-UIC05).
/// Asserts:
/// 1. Every Controls/**/*.xaml file in src/MuscleCuties.UI/Controls is referenced by at least one Page, Control, or C# file.
/// 2. Every route registered in AppShell.xaml.cs points to an existing Page file in src/MuscleCuties.UI/Pages.
/// 3. PhaseAdjustmentReviewPageV010 is registered in both AppShell.xaml.cs and MauiProgram.cs.
/// 4. Retired controls and pages (DashboardCalendarCard, InjuryLogPage, ScoreToColorConverter, DashboardMonthGrid) do not exist.
/// </summary>
public class UiCleanlinessAndReachabilityTests
{
    private static readonly string RepoRoot = FindRepositoryRoot();

    private static readonly Regex RouteRegistrationRegex = new(
        @"Routing\.RegisterRoute\s*\(\s*nameof\s*\(\s*([A-Za-z0-9_]+)\s*\)\s*,\s*typeof\s*\(\s*([A-Za-z0-9_]+)\s*\)\s*\)",
        RegexOptions.Compiled);

    [Fact]
    public void Every_xaml_control_is_referenced_by_at_least_one_page_or_control_or_cs_file()
    {
        var controlsDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls");
        Assert.True(Directory.Exists(controlsDir), $"Controls directory not found at {controlsDir}");

        var xamlControls = Directory.GetFiles(controlsDir, "*.xaml", SearchOption.AllDirectories);
        Assert.NotEmpty(xamlControls);

        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var candidateSearchFiles = Directory.GetFiles(uiDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        (f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var unreferenced = new List<string>();

        foreach (var controlPath in xamlControls)
        {
            var controlName = Path.GetFileNameWithoutExtension(controlPath);
            var controlCsPath = controlPath + ".cs";

            var hasReference = false;
            foreach (var searchFile in candidateSearchFiles)
            {
                if (string.Equals(searchFile, controlPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(searchFile, controlCsPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var content = File.ReadAllText(searchFile);
                if (content.Contains(controlName, StringComparison.Ordinal))
                {
                    hasReference = true;
                    break;
                }
            }

            if (!hasReference)
            {
                unreferenced.Add(Path.GetRelativePath(RepoRoot, controlPath));
            }
        }

        Assert.True(
            unreferenced.Count == 0,
            $"Found {unreferenced.Count} unreferenced XAML control(s) in src/MuscleCuties.UI/Controls:\n" +
            string.Join("\n", unreferenced));
    }

    [Fact]
    public void Every_route_registered_in_AppShell_points_to_an_existing_page_file()
    {
        var appShellPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "AppShell.xaml.cs");
        Assert.True(File.Exists(appShellPath), $"AppShell.xaml.cs not found at {appShellPath}");

        var content = File.ReadAllText(appShellPath);
        var matches = RouteRegistrationRegex.Matches(content);
        Assert.NotEmpty(matches);

        var pagesDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages");
        Assert.True(Directory.Exists(pagesDir), $"Pages directory not found at {pagesDir}");

        var missingPages = new List<string>();

        foreach (Match match in matches)
        {
            var routeName = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;
            Assert.Equal(routeName, typeName);

            var pageFiles = Directory.GetFiles(pagesDir, $"{typeName}.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                            !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .ToList();

            if (pageFiles.Count == 0)
            {
                missingPages.Add(typeName);
            }
        }

        Assert.True(
            missingPages.Count == 0,
            $"Found {missingPages.Count} route(s) registered in AppShell.xaml.cs with no matching Page file:\n" +
            string.Join("\n", missingPages));
    }

    [Fact]
    public void PhaseAdjustmentReviewPageV010_is_registered_in_AppShell_and_MauiProgram()
    {
        var appShellPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "AppShell.xaml.cs");
        var appShellContent = File.ReadAllText(appShellPath);
        Assert.Contains(
            "Routing.RegisterRoute(nameof(PhaseAdjustmentReviewPageV010), typeof(PhaseAdjustmentReviewPageV010));",
            appShellContent);

        var mauiProgramPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "MauiProgram.cs");
        var mauiContent = File.ReadAllText(mauiProgramPath);
        Assert.Contains("services.AddTransient<PhaseAdjustmentReviewPageV010>();", mauiContent);
        Assert.Contains("services.AddTransient<PhaseAdjustmentReviewViewModelV010>", mauiContent);
    }

    [Fact]
    public void Retired_controls_and_pages_do_not_exist_on_disk()
    {
        string[] retiredPaths =
        [
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Dashboard", "DashboardCalendarCard.xaml"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Dashboard", "DashboardCalendarCard.xaml.cs"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Dashboard", "DashboardMonthGrid.cs"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Models", "UI", "Dashboard", "DashboardCalendarDay.cs"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels", "Dashboard", "DashboardViewModel.Calendar.cs"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Profile", "InjuryLogPage.xaml"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Profile", "InjuryLogPage.xaml.cs"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Converters", "ScoreToColorConverter.cs"),
            Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Models", "UI", "Profile", "PreferenceItem.cs")
        ];

        foreach (var path in retiredPaths)
        {
            Assert.False(File.Exists(path), $"Retired file should not exist: {Path.GetRelativePath(RepoRoot, path)}");
        }
    }

    [Fact]
    public void Single_font_family_Inter_is_enforced_across_MauiProgram_Styles_and_all_XAML_files()
    {
        var mauiProgramPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "MauiProgram.cs");
        var mauiContent = File.ReadAllText(mauiProgramPath);
        Assert.Contains("fonts.AddFont(\"OpenSans-Regular.ttf\", \"Inter\");", mauiContent);
        Assert.DoesNotContain("OpenSansSemibold", mauiContent);

        var stylesPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Styles.xaml");
        var stylesContent = File.ReadAllText(stylesPath);
        Assert.Contains("<x:String x:Key=\"AppFontFamily\">Inter</x:String>", stylesContent);
        Assert.DoesNotContain("Property=\"FontFamily\" Value=\"Inter\"", stylesContent);

        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var pageAndControlXamlFiles = Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !string.Equals(f, stylesPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var inlineFontFamilyViolations = new List<string>();
        foreach (var xamlFile in pageAndControlXamlFiles)
        {
            var content = File.ReadAllText(xamlFile);
            if (content.Contains("FontFamily=", StringComparison.Ordinal) ||
                content.Contains("Property=\"FontFamily\"", StringComparison.Ordinal))
            {
                inlineFontFamilyViolations.Add(Path.GetRelativePath(RepoRoot, xamlFile));
            }
        }

        Assert.True(
            inlineFontFamilyViolations.Count == 0,
            $"Found {inlineFontFamilyViolations.Count} XAML file(s) outside Styles.xaml with inline FontFamily declarations:\n" +
            string.Join("\n", inlineFontFamilyViolations));
    }

    [Fact]
    public void All_xaml_static_resource_keys_are_defined_in_app_or_local_resources()
    {
        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        string[] globalResourceFiles =
        [
            Path.Combine(uiDir, "Resources", "Styles", "Colors.xaml"),
            Path.Combine(uiDir, "Resources", "Styles", "Styles.xaml"),
            Path.Combine(uiDir, "Resources", "Styles", "WorkoutPlanningResources.xaml"),
            Path.Combine(uiDir, "App.xaml")
        ];

        var keyDefinitionRegex = new Regex(@"x:Key=""([^""]+)""", RegexOptions.Compiled);
        var staticResourceUsageRegex = new Regex(@"\{StaticResource\s+(?:Key=)?([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

        var globalKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var globalFile in globalResourceFiles)
        {
            Assert.True(File.Exists(globalFile), $"Global XAML resource file not found: {globalFile}");
            var globalContent = File.ReadAllText(globalFile);
            foreach (Match match in keyDefinitionRegex.Matches(globalContent))
            {
                globalKeys.Add(match.Groups[1].Value);
            }
        }

        var allXamlFiles = Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        var unresolvedUsages = new List<string>();
        foreach (var xamlFile in allXamlFiles)
        {
            var content = File.ReadAllText(xamlFile);
            var localKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in keyDefinitionRegex.Matches(content))
            {
                localKeys.Add(match.Groups[1].Value);
            }

            foreach (Match usage in staticResourceUsageRegex.Matches(content))
            {
                var key = usage.Groups[1].Value;
                if (!globalKeys.Contains(key) && !localKeys.Contains(key))
                {
                    unresolvedUsages.Add($"{Path.GetRelativePath(RepoRoot, xamlFile)} -> {key}");
                }
            }
        }

        Assert.True(
            unresolvedUsages.Count == 0,
            $"Found {unresolvedUsages.Count} unresolved StaticResource key(s) in XAML:\n" +
            string.Join("\n", unresolvedUsages.Distinct(StringComparer.Ordinal)));
    }

    [Fact]
    public void MealSuggestionModal_does_not_contain_obsolete_embedded_picker_or_choose_meal_button()
    {
        var path = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Nutrition", "MealSuggestionModal.xaml");
        Assert.True(File.Exists(path), $"MealSuggestionModal.xaml not found at {path}");

        var content = File.ReadAllText(path);
        Assert.DoesNotContain("Nutrition_MealSuggestion_ChooseMeal", content);
        Assert.DoesNotContain("BackToMealTypePickerCommand", content);
        Assert.DoesNotContain("Nutrition_MealSuggestion_ChooseAMeal", content);
        Assert.DoesNotContain("IsMealTypePickerVisible", content);
    }

    [Fact]
    public void WorkoutActivityCard_gates_optional_button_with_CanMarkOptional()
    {
        var path = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Workout", "WorkoutActivityCard.xaml");
        Assert.True(File.Exists(path), $"WorkoutActivityCard.xaml not found at {path}");

        var content = File.ReadAllText(path);
        Assert.Contains("IsVisible=\"{Binding CanMarkOptional}\"", content);
        Assert.Contains("Style=\"{StaticResource ButtonSecondary}\"", content);
    }

    [Fact]
    public void Shared_styles_enforce_2_button_options_unified_inputs_and_3_card_levels()
    {
        var stylesPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Styles.xaml");
        Assert.True(File.Exists(stylesPath), $"Styles.xaml not found at {stylesPath}");

        var stylesContent = File.ReadAllText(stylesPath);

        // 1. Two primary/secondary button options across the app (+ ButtonLink for inline links)
        Assert.Contains("x:Key=\"ButtonPrimary\"", stylesContent);
        Assert.Contains("x:Key=\"ButtonSecondary\"", stylesContent);
        Assert.Contains("x:Key=\"ButtonLink\"", stylesContent);
        Assert.Contains("<Style TargetType=\"Button\">", stylesContent);

        // 2. Unified input container fields across all pages
        Assert.Contains("x:Key=\"InputContainer\"", stylesContent);
        Assert.Contains("x:Key=\"PickerContainer\"", stylesContent);
        Assert.Contains("x:Key=\"EditorContainer\"", stylesContent);

        // 3. Card surface logic: CardPrimary (main containers), CardSecondary/SurfaceTile (inner sections), PillBadge (badges)
        Assert.Contains("x:Key=\"CardPrimary\"", stylesContent);
        Assert.Contains("x:Key=\"CardSecondary\"", stylesContent);
        Assert.Contains("x:Key=\"SurfaceTile\"", stylesContent);
        Assert.Contains("x:Key=\"PillBadge\"", stylesContent);
    }

    [Fact]
    public void All_input_elements_are_wrapped_in_standardized_containers()
    {
        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var xamlFiles = Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        var allowedContainerTokens = new[]
        {
            "InputContainer",
            "PickerContainer",
            "EditorContainer",
            "WorkoutMetricInput",
            "ProfileFieldSurface",
            "PlanFieldSurface",
            "PlanPickerSurface",
            "ProfileMeasurementPickerSurface",
            "ProfileMeasurementChoiceSurface",
            "CardPrimary",
            "CardSecondary",
            "SurfaceTile"
        };

        var nakedInputs = new List<string>();

        foreach (var file in xamlFiles)
        {
            var lines = File.ReadAllLines(file);
            for (var idx = 0; idx < lines.Length; idx++)
            {
                var stripped = lines[idx].Trim();
                if (stripped.StartsWith("<Entry", StringComparison.Ordinal) ||
                    stripped.StartsWith("<Editor", StringComparison.Ordinal) ||
                    stripped.StartsWith("<Picker", StringComparison.Ordinal) ||
                    stripped.StartsWith("<DatePicker", StringComparison.Ordinal) ||
                    stripped.StartsWith("<TimePicker", StringComparison.Ordinal))
                {
                    var lookbackStart = Math.Max(0, idx - 5);
                    var lookbackText = string.Join("\n", lines[lookbackStart..idx]);
                    var hasContainer = allowedContainerTokens.Any(token => lookbackText.Contains(token, StringComparison.Ordinal));
                    if (!hasContainer)
                    {
                        var relativePath = Path.GetRelativePath(RepoRoot, file);
                        nakedInputs.Add($"{relativePath}:{idx + 1} -> {stripped}");
                    }
                }
            }
        }

        Assert.True(nakedInputs.Count == 0,
            $"Found {nakedInputs.Count} uncontained input elements:\n" + string.Join("\n", nakedInputs));
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
