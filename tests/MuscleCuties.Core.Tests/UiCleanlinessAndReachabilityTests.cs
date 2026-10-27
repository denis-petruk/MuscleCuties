using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Programmatic verification of UI Cleanliness, Control Usage, and Route Reachability (UIC01-UIC05).
/// Asserts:
/// 1. Every Controls/**/*.xaml file in src/MuscleCuties.UI/Controls is referenced by at least one Page, Control, or C# file.
/// 2. Every route registered in AppShell.xaml.cs points to an existing Page file in src/MuscleCuties.UI/Pages.
/// 3. The retired phase-adjustment review is not registered for navigation or dependency injection.
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
    public void RetiredPhaseAdjustmentReview_is_not_reachable_through_navigation_or_dependency_injection()
    {
        var appShellPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "AppShell.xaml.cs");
        var appShellContent = File.ReadAllText(appShellPath);
        Assert.DoesNotContain(
            "Routing.RegisterRoute(nameof(PhaseAdjustmentReviewPageV010), typeof(PhaseAdjustmentReviewPageV010));",
            appShellContent);

        var mauiProgramPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "MauiProgram.cs");
        var mauiContent = File.ReadAllText(mauiProgramPath);
        Assert.DoesNotContain("services.AddTransient<PhaseAdjustmentReviewPageV010>();", mauiContent);
        Assert.DoesNotContain("services.AddTransient<PhaseAdjustmentReviewViewModelV010>", mauiContent);
        Assert.DoesNotContain("services.AddSingleton<IPhaseAdjustmentProposalClientV010", mauiContent);
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
        var colorsPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Colors.xaml");
        var colorsContent = File.ReadAllText(colorsPath);
        Assert.Contains("<x:String x:Key=\"AppFontFamily\">Inter</x:String>", colorsContent);
        Assert.Contains("Value=\"{StaticResource AppFontFamily}\"", stylesContent);
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
    public void Workout_preferences_carry_onboarding_duration_without_duplicate_picker_and_use_query_first_exclusions()
    {
        var activityPickerPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Workout",
            "WorkoutActivityPreferencePicker.xaml");
        var preferencesChoicesPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Profile",
            "ProfileWorkoutPreferencesChoices.xaml");
        var preferencesViewModelPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels", "Profile",
            "ProfileWorkoutPreferencesViewModel.cs");
        var registrationStorePath = Path.Combine(RepoRoot, "src", "MuscleCuties.Infrastructure.PostgreSql", "Account",
            "Registration", "PostgresCompleteAccountRegistrationStore.cs");

        var activityPicker = File.ReadAllText(activityPickerPath);
        var preferencesChoices = File.ReadAllText(preferencesChoicesPath);
        var preferencesViewModel = File.ReadAllText(preferencesViewModelPath);
        var registrationStore = File.ReadAllText(registrationStorePath);

        Assert.DoesNotContain("<Picker", activityPicker);
        Assert.DoesNotContain("ShowSessionDurationPicker", activityPicker);
        Assert.DoesNotContain("SessionDuration", preferencesChoices);
        Assert.DoesNotContain("SelectedSessionDurationMinutes", preferencesViewModel);
        Assert.Contains("ResolveSessionDurationMinutes(option.ActivityType)", preferencesViewModel);
        Assert.Contains("_savedActivitySessionDurationsMinutes", preferencesViewModel);
        Assert.Contains("QuizQuestionType.SessionDuration", registrationStore);
        Assert.Contains("RegistrationActivityDurationDefaults.ForSelectedActivities", registrationStore);
        Assert.Contains("{Binding HasExerciseExclusionQuery}", preferencesChoices);
        Assert.Contains("{Binding SelectedExerciseExclusions}", preferencesChoices);
        Assert.Contains("{Binding HasNoExerciseExclusionMatches}", preferencesChoices);
    }

    [Fact]
    public void Strength_plan_setup_uses_saved_strength_choice_without_a_second_picker()
    {
        var generationPagePath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Workout",
            "StrengthPlanGenerationPage.xaml");
        var generationViewModelPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels", "Workout",
            "StrengthPlanGenerationViewModel.cs");

        var generationPage = File.ReadAllText(generationPagePath);
        var generationViewModel = File.ReadAllText(generationViewModelPath);

        Assert.DoesNotContain("BindableLayout.ItemsSource=\"{Binding Activities}\"", generationPage);
        Assert.DoesNotContain("SelectActivityCommand", generationPage);
        Assert.Contains("IsVisible=\"{Binding HasSavedStrengthActivity}\"", generationPage);
        Assert.Contains("Text=\"{Binding SelectedActivity.Label}\"", generationPage);
        Assert.Contains("var savedStrengthActivity = preferences.ActivityTypes", generationViewModel);
        Assert.Contains(".Where(StrengthActivities.Contains)", generationViewModel);
        Assert.DoesNotContain("SelectActivityCommand", generationViewModel);
        Assert.DoesNotContain("initialActivities.Add((int)WorkoutActivityDto.GeneralStrength)", generationViewModel);
    }

    [Fact]
    public void Workout_load_retry_and_apply_navigation_states_are_wired()
    {
        var preferencesPage = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Profile",
            "ProfileWorkoutPreferencesPage.xaml"));
        var generationPage = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Workout",
            "StrengthPlanGenerationPage.xaml"));
        var appNavigation = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "MauiProgram.cs"));
        var generationViewModel = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels",
            "Workout", "StrengthPlanGenerationViewModel.cs"));

        Assert.Contains("<sharedControls:PageLoadingOverlay", preferencesPage);
        Assert.Contains("<sharedControls:PageLoadingOverlay", generationPage);
        Assert.Contains("Command=\"{Binding OpenTrainCommand}\"", generationPage);
        Assert.Contains("IsProposalAccepted", generationViewModel);
        Assert.Contains("IsTrainNavigationPending", generationViewModel);
        Assert.Contains("NavigateAfterOnboardingAsync(sp)", appNavigation);
        Assert.Contains("NavigateToAsync(nameof(StrengthPlanGenerationPage))", appNavigation);
    }

    [Fact]
    public void Shared_styles_enforce_2_button_styles_4_card_styles_and_shared_surfaces()
    {
        var stylesPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Styles.xaml");
        Assert.True(File.Exists(stylesPath), $"Styles.xaml not found at {stylesPath}");

        var stylesContent = File.ReadAllText(stylesPath);

        // 1. Exactly two button styles own button appearance across the app.
        Assert.Contains("x:Key=\"ButtonPrimary\"", stylesContent);
        Assert.Contains("x:Key=\"ButtonSecondary\"", stylesContent);
        Assert.Equal(2, Regex.Matches(stylesContent, "<Style[^>]*TargetType=\"Button\"").Count);

        // 2. Unified input and search containers across all pages.
        Assert.Contains("x:Key=\"InputContainer\"", stylesContent);
        Assert.Contains("x:Key=\"PickerContainer\"", stylesContent);
        Assert.Contains("x:Key=\"EditorContainer\"", stylesContent);
        Assert.Contains("x:Key=\"SearchContainer\"", stylesContent);

        // 3. Four canonical card styles; selection indicators and badges remain utility styles.
        Assert.Contains("x:Key=\"CardPrimary\"", stylesContent);
        Assert.Contains("x:Key=\"CardSecondary\"", stylesContent);
        Assert.Contains("x:Key=\"CardAccent\"", stylesContent);
        Assert.Contains("x:Key=\"SelectableCard\"", stylesContent);
        Assert.Equal(4, Regex.Matches(stylesContent, "<Style x:Key=\"(?:CardPrimary|CardSecondary|CardAccent|SelectableCard)\"").Count);
        Assert.True(Regex.Matches(stylesContent, "<Style x:Key=\"ModalSurface\"").Count == 1);
        Assert.True(Regex.Matches(stylesContent, "<Style x:Key=\"SearchContainer\"").Count == 1);
        Assert.Contains("x:Key=\"SelectionRadioRing\"", stylesContent);
        Assert.Contains("x:Key=\"SelectionCheckbox\"", stylesContent);
    }

    [Fact]
    public void All_inputs_use_shared_profile_setup_appearance()
    {
        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var inputNames = new HashSet<string> { "Entry", "Editor", "Picker", "DatePicker", "TimePicker" };
        var appearanceProperties = new HashSet<string>
        {
            "BackgroundColor", "TextColor", "PlaceholderColor", "TitleColor", "FontSize",
            "FontFamily", "FontAttributes", "HeightRequest", "MinimumHeightRequest",
            "Padding", "Margin", "Stroke", "StrokeThickness", "CornerRadius",
            "HorizontalTextAlignment"
        };
        var allowedSurfaces = new HashSet<string>
        {
            "{StaticResource InputContainer}", "{StaticResource PickerContainer}",
            "{StaticResource EditorContainer}", "{StaticResource SearchContainer}"
        };

        foreach (var file in Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                    !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var document = XDocument.Load(file);
            foreach (var input in document.Descendants().Where(element => inputNames.Contains(element.Name.LocalName)))
            {
                var appearanceOverrides = input.Attributes()
                    .Where(attribute => appearanceProperties.Contains(attribute.Name.LocalName))
                    .Select(attribute => attribute.Name.LocalName)
                    .ToArray();
                Assert.Empty(appearanceOverrides);

                var surface = input.Ancestors().FirstOrDefault(element => element.Name.LocalName == "Border");
                Assert.NotNull(surface);
                var style = surface.Attribute("Style")?.Value;
                Assert.NotNull(style);
                Assert.Contains(style, allowedSurfaces);
            }
        }
    }

    [Fact]
    public void Visual_styles_are_defined_in_shared_styles_file()
    {
        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        foreach (var file in Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                    !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                                    !file.EndsWith($"{Path.DirectorySeparatorChar}Styles.xaml", StringComparison.Ordinal)))
        {
            var document = XDocument.Load(file);
            Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Style");
        }
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
            "SearchContainer"
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

    [Fact]
    public void Xaml_buttons_use_shared_styles_without_local_appearance_overrides()
    {
        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var xamlFiles = Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.EndsWith("Styles.xaml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var buttonRegex = new Regex(@"<Button\b(?!\.)([^>]*)/?>", RegexOptions.Singleline | RegexOptions.Compiled);
        var appearanceRegex = new Regex(
            @"\b(?:BackgroundColor|TextColor|FontFamily|FontSize|FontAttributes|CornerRadius|BorderColor|BorderWidth|Padding|HeightRequest|MinimumHeightRequest|MinimumWidthRequest)\s*=",
            RegexOptions.Compiled);
        var styleRegex = new Regex(@"\bStyle\s*=\s*""\{StaticResource Button(?:Primary|Secondary)\}""", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in xamlFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in buttonRegex.Matches(content))
            {
                var buttonAttributes = match.Groups[1].Value;
                if (appearanceRegex.IsMatch(buttonAttributes) || !styleRegex.IsMatch(buttonAttributes))
                {
                    var relativePath = Path.GetRelativePath(RepoRoot, file);
                    violations.Add($"{relativePath}: {match.Value.Split('\n')[0]}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} Button(s) without one shared button style or with local appearance overrides:\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void Canonical_cards_have_no_local_surface_overrides()
    {
        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var cardStyles = new HashSet<string>(StringComparer.Ordinal)
        {
            "{StaticResource CardPrimary}", "{StaticResource CardSecondary}",
            "{StaticResource CardAccent}", "{StaticResource SelectableCard}"
        };
        var surfaceProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "BackgroundColor", "Background", "Stroke", "StrokeShape", "StrokeThickness",
            "Padding", "Shadow", "CornerRadius"
        };
        var violations = new List<string>();

        foreach (var file in Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                    !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                                    !file.EndsWith($"{Path.DirectorySeparatorChar}Styles.xaml", StringComparison.Ordinal)))
        {
            var document = XDocument.Load(file);
            foreach (var border in document.Descendants().Where(element =>
                         element.Name.LocalName == "Border" &&
                         cardStyles.Contains(element.Attribute("Style")?.Value ?? string.Empty)))
            {
                foreach (var attribute in border.Attributes().Where(attribute => surfaceProperties.Contains(attribute.Name.LocalName)))
                {
                    var isDataBoundState = (attribute.Name.LocalName is "BackgroundColor" or "Background" or "Stroke") &&
                                           attribute.Value.Contains("{Binding", StringComparison.Ordinal);
                    if (!isDataBoundState)
                    {
                        violations.Add($"{Path.GetRelativePath(RepoRoot, file)}: {attribute.Name.LocalName}");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} local appearance override(s) on canonical cards:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void All_xaml_StaticResource_references_are_defined_and_Styles_xaml_has_no_forward_references()
    {
        var uiDir = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var xamlFiles = Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        var keyDefRegex = new Regex(@"x:Key=""([^""]+)""", RegexOptions.Compiled);
        var staticResRegex = new Regex(@"\{\s*(?:StaticResource|AppThemeBinding\s+(?:Light|Dark)=\{\s*StaticResource)\s+([^}, ]+)", RegexOptions.Compiled);

        var definedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in xamlFiles)
        {
            foreach (var line in File.ReadLines(file))
            {
                foreach (Match match in keyDefRegex.Matches(line))
                {
                    definedKeys.Add(match.Groups[1].Value);
                }
            }
        }

        var missing = new List<string>();
        foreach (var file in xamlFiles)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in staticResRegex.Matches(lines[i]))
                {
                    var key = match.Groups[1].Value.Trim();
                    if (!definedKeys.Contains(key))
                    {
                        missing.Add($"{Path.GetRelativePath(RepoRoot, file)}:{i + 1} uses undefined key '{key}'");
                    }
                }
            }
        }

        Assert.True(missing.Count == 0,
            $"Found {missing.Count} undefined StaticResource reference(s):\n" + string.Join("\n", missing));

        var stylesPath = Path.Combine(uiDir, "Resources", "Styles", "Styles.xaml");
        var colorsPath = Path.Combine(uiDir, "Resources", "Styles", "Colors.xaml");
        var forwardRefIssues = new List<string>();

        if (File.Exists(stylesPath))
        {
            var availableKeys = new HashSet<string>(StringComparer.Ordinal) { "x:Null" };
            if (File.Exists(colorsPath))
            {
                foreach (var line in File.ReadLines(colorsPath))
                {
                    foreach (Match match in keyDefRegex.Matches(line))
                    {
                        availableKeys.Add(match.Groups[1].Value);
                    }
                }
            }

            var lines = File.ReadAllLines(stylesPath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (Match match in staticResRegex.Matches(line))
                {
                    var key = match.Groups[1].Value.Trim();
                    if (!availableKeys.Contains(key))
                    {
                        forwardRefIssues.Add($"Styles.xaml:{i + 1} references '{key}' before it is defined");
                    }
                }

                foreach (Match match in keyDefRegex.Matches(line))
                {
                    availableKeys.Add(match.Groups[1].Value);
                }
            }
        }

        Assert.True(forwardRefIssues.Count == 0,
            $"Found {forwardRefIssues.Count} forward reference(s) in Styles.xaml:\n" + string.Join("\n", forwardRefIssues));
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
