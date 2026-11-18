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
        Assert.Contains("fonts.AddFont(\"OpenSans-Semibold.ttf\", \"InterSemibold\");", mauiContent);

        var stylesPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Styles.xaml");
        var stylesContent = File.ReadAllText(stylesPath);
        var colorsPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Colors.xaml");
        var colorsContent = File.ReadAllText(colorsPath);
        Assert.Contains("<x:String x:Key=\"AppFontFamily\">Inter</x:String>", colorsContent);
        Assert.Contains("<x:String x:Key=\"AppFontSemibold\">InterSemibold</x:String>", colorsContent);
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
    public void Auth_pages_anchor_the_account_switch_footer_and_use_the_shared_back_header()
    {
        var authRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Auth");
        var login = File.ReadAllText(Path.Combine(authRoot, "LoginPage.xaml"));
        var register = File.ReadAllText(Path.Combine(authRoot, "RegisterPage.xaml"));

        Assert.Contains("LoginCreateAccountRow", login);
        Assert.Contains("Grid.Row=\"2\"", login);
        Assert.DoesNotContain("UiInsets0_24_0_0", login);

        Assert.Contains("<sharedControls:PageHeader", register);
        Assert.Contains("BackCommand=\"{Binding GoBackCommand}\"", register);

        foreach (var page in new[] { "LoginPage.xaml", "RegisterPage.xaml", "OtpVerificationPage.xaml", "PasswordRecoveryPage.xaml" })
            Assert.Contains("<sharedControls:PageHeader", File.ReadAllText(Path.Combine(authRoot, page)));

        var recovery = File.ReadAllText(Path.Combine(authRoot, "PasswordRecoveryPage.xaml"));
        var document = XDocument.Parse(recovery);
        var rootGrid = document.Root!.Elements().Single(element => element.Name.LocalName == "Grid");
        Assert.Equal("Auto,*,Auto", rootGrid.Attribute("RowDefinitions")?.Value);

        var backToLogin = recovery.IndexOf("Auth_PasswordRecovery_BackToLogIn", StringComparison.Ordinal);
        Assert.True(backToLogin >= 0, "Password recovery must keep its Back to Log In action.");
        var footerStart = recovery.LastIndexOf("<Grid", backToLogin, StringComparison.Ordinal);
        Assert.True(footerStart >= 0, "Back to Log In must live in an anchored footer.");
        var footer = recovery[footerStart..backToLogin];
        Assert.Contains("Grid.Row=\"2\"", footer, StringComparison.Ordinal);
        Assert.Contains("PresentationActionFooterPadding", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void Auth_account_switch_footers_wrap_at_large_text()
    {
        var authRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Auth");
        var login = XDocument.Load(Path.Combine(authRoot, "LoginPage.xaml"));
        var loginFooter = login.Descendants().Single(element =>
            (string?)element.Attribute("AutomationId") == "LoginCreateAccountRow");
        Assert.Equal("FlexLayout", loginFooter.Name.LocalName);
        Assert.Equal("Wrap", loginFooter.Attribute("Wrap")?.Value);
        Assert.Equal("Center", loginFooter.Attribute("JustifyContent")?.Value);

        var register = XDocument.Load(Path.Combine(authRoot, "RegisterPage.xaml"));
        var registerPrompt = register.Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{loc:LocalizedText Auth_Register_AlreadyHaveAnAccount}");
        var registerWrap = registerPrompt.Ancestors().FirstOrDefault(ancestor =>
            ancestor.Name.LocalName == "FlexLayout");
        Assert.NotNull(registerWrap);
        Assert.Equal("Wrap", registerWrap!.Attribute("Wrap")?.Value);
    }

    [Fact]
    public void Onboarding_progress_headers_use_the_shared_header_without_losing_progress()
    {
        var onboardingRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Onboarding");
        var profileSetup = File.ReadAllText(Path.Combine(onboardingRoot, "ProfileSetupPage.xaml"));
        var quiz = File.ReadAllText(Path.Combine(onboardingRoot, "QuizPage.xaml"));

        Assert.Contains("<sharedControls:PageHeader", profileSetup, StringComparison.Ordinal);
        Assert.Contains("PageHeader.EyebrowContent", profileSetup, StringComparison.Ordinal);
        Assert.Contains("Onboarding_ProfileSetup_StepOneOfTwo", profileSetup, StringComparison.Ordinal);
        Assert.Contains("Onboarding_ProfileSetup_ThisShapesYourFirstTrainingAnd", profileSetup, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{StaticResource PageTitle}\"", profileSetup, StringComparison.Ordinal);

        Assert.Contains("<sharedControls:PageHeader", quiz, StringComparison.Ordinal);
        Assert.Contains("PageHeader.EyebrowContent", quiz, StringComparison.Ordinal);
        Assert.Contains("PageHeader.TitleIcon", quiz, StringComparison.Ordinal);
        Assert.Contains("Onboarding_Quiz_StepTwoOfTwo", quiz, StringComparison.Ordinal);
        Assert.Contains("<ProgressBar", quiz, StringComparison.Ordinal);
        Assert.Contains("ProgressText", quiz, StringComparison.Ordinal);
        Assert.Contains("Onboarding_Quiz_GoBackToProfile", quiz, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{StaticResource PageTitle}\"", quiz, StringComparison.Ordinal);
    }

    [Fact]
    public void Onboarding_photo_heading_wraps_at_large_text()
    {
        var profileSetup = XDocument.Load(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages",
            "Onboarding", "ProfileSetupPage.xaml"));
        var optional = profileSetup.Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{loc:LocalizedText Onboarding_ProfileSetup_Optional}");
        var photoHeading = optional.Ancestors().FirstOrDefault(ancestor =>
            ancestor.Name.LocalName == "FlexLayout");
        Assert.NotNull(photoHeading);
        Assert.Equal("Wrap", photoHeading!.Attribute("Wrap")?.Value);
    }

    [Fact]
    public void Activity_card_places_the_icon_under_the_day_beside_the_title()
    {
        var card = XDocument.Load(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls",
            "Workout", "WorkoutActivityCard.xaml"));
        var dayLabel = card.Descendants().Single(element =>
            element.Name.LocalName == "Label" &&
            (string?)element.Attribute("Text") ==
            "{Binding DayLabel, Converter={StaticResource LocalizedStringConverter}}");
        var dayTile = dayLabel.Ancestors().First(ancestor => ancestor.Name.LocalName == "Border");
        var titleLabel = card.Descendants().Single(element =>
            element.Name.LocalName == "Label" &&
            (string?)element.Attribute("Text") ==
            "{Binding Title, Converter={StaticResource LocalizedStringConverter}}");
        var titleRow = titleLabel.Ancestors().First(ancestor => ancestor.Name.LocalName == "FlexLayout");
        var icon = card.Descendants().Single(element =>
            element.Name.LocalName == "Image" &&
            ((string?)element.Attribute("Source"))?.Contains("Binding IconGlyph", StringComparison.Ordinal) == true);
        var cardGrid = dayTile.Parent;

        // Both are direct children of the card grid, so the icon can never sit inside the day badge.
        Assert.Same(cardGrid, icon.Parent);
        Assert.Same(cardGrid, titleRow.Parent);

        // Under the badge: a later row, in the badge's own column.
        var dayRow = EffectiveGridRow(dayTile);
        var iconRow = EffectiveGridRow(icon);
        Assert.True(iconRow > dayRow, "The activity icon must sit on a row below the day badge.");
        Assert.Equal(EffectiveGridColumn(dayTile), EffectiveGridColumn(icon));

        // Beside the title: the same row as the title, starting to the right of the icon.
        Assert.Equal(iconRow, EffectiveGridRow(titleRow));
        Assert.True(EffectiveGridColumn(titleRow) > EffectiveGridColumn(icon),
            "The workout title must start to the right of the activity icon.");
    }

    [Fact]
    public void Activity_card_badge_and_actions_grow_instead_of_clipping_at_large_text()
    {
        var card = XDocument.Load(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls",
            "Workout", "WorkoutActivityCard.xaml"));
        var dayLabel = card.Descendants().Single(element =>
            element.Name.LocalName == "Label" &&
            (string?)element.Attribute("Text") ==
            "{Binding DayLabel, Converter={StaticResource LocalizedStringConverter}}");
        var dayTile = dayLabel.Ancestors().First(ancestor => ancestor.Name.LocalName == "Border");

        // A fixed 36pt tile cannot hold a two-letter day once the type scales: the abbreviation
        // either breaks inside itself or loses its second letter under the tile's edge. The
        // reviewed 36pt stays as the resting size, expressed as a minimum the tile may outgrow.
        Assert.Null(dayTile.Attribute("WidthRequest"));
        Assert.Null(dayTile.Attribute("HeightRequest"));
        Assert.Equal("{StaticResource UiDimension36}", (string?)dayTile.Attribute("MinimumWidthRequest"));
        Assert.Equal("{StaticResource UiDimension36}", (string?)dayTile.Attribute("MinimumHeightRequest"));
        Assert.Equal("NoWrap", (string?)dayLabel.Attribute("LineBreakMode"));

        // The session actions were measured against an unbounded row, so a long Russian label ran
        // past the card edge instead of wrapping. A wrapping flex row measures against the card.
        var requiredAction = card.Descendants().Single(element =>
            element.Name.LocalName == "Button" &&
            ((string?)element.Attribute("Text"))?.Contains(
                "Binding OptionalStatusText", StringComparison.Ordinal) == true);
        var actionRow = requiredAction.Ancestors().First(ancestor =>
            ancestor.Name.LocalName is "FlexLayout" or "HorizontalStackLayout" or "VerticalStackLayout");
        Assert.Equal("FlexLayout", actionRow.Name.LocalName);
        Assert.Equal("Wrap", (string?)actionRow.Attribute("Wrap"));

        var easierAction = card.Descendants().Single(element =>
            element.Name.LocalName == "Button" &&
            ((string?)element.Attribute("Text"))?.Contains(
                "Binding EasierAlternativeStatusText", StringComparison.Ordinal) == true);
        Assert.Equal(actionRow, easierAction.Parent);
        Assert.Equal("{StaticResource ButtonSecondary}", (string?)requiredAction.Attribute("Style"));
        Assert.Equal("{StaticResource ButtonSecondary}", (string?)easierAction.Attribute("Style"));
    }

    [Fact]
    public void Activity_card_actions_take_the_whole_card_at_large_text()
    {
        var card = XDocument.Load(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls",
            "Workout", "WorkoutActivityCard.xaml"));
        var requiredAction = card.Descendants().Single(element =>
            element.Name.LocalName == "Button" &&
            ((string?)element.Attribute("Text"))?.Contains(
                "Binding OptionalStatusText", StringComparison.Ordinal) == true);
        var actionRow = requiredAction.Ancestors().First(ancestor =>
            ancestor.Name.LocalName is "FlexLayout" or "HorizontalStackLayout" or "VerticalStackLayout");

        // A row placed only in the title column starts after the day tile and its spacing, so at
        // accessibility sizes its longest word ("Обязательная") has less width than the word needs
        // and breaks inside itself. The actions span both columns to keep the card's inner width.
        Assert.Equal("FlexLayout", actionRow.Name.LocalName);
        Assert.Equal("0", (string?)actionRow.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)actionRow.Attribute("Grid.ColumnSpan"));
    }

    [Fact]
    public void Week_chips_stay_reachable_when_the_chosen_week_has_no_days()
    {
        var page = XDocument.Load(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages",
            "Workout", "WorkoutPage.xaml"));
        var weekCommand = page.Descendants().Single(element =>
            element.Name.LocalName == "TapGestureRecognizer" &&
            ((string?)element.Attribute("Command"))?.Contains(
                "SelectWeekChipCommand", StringComparison.Ordinal) == true);
        var weekRow = weekCommand.Ancestors().First(ancestor => ancestor.Name.LocalName == "ScrollView");

        // Hiding the row with the selected week's days removed the only way back to a week that
        // has sessions: tapping a week without sessions made the whole schedule block vanish and
        // the reader was left with a blank page. A plan with weeks always keeps its chips.
        Assert.Equal("{Binding HasPlanWeeks}", (string?)weekRow.Attribute("IsVisible"));
    }

    [Fact]
    public void Empty_workouts_state_is_part_of_the_page_flow_with_catalog_copy()
    {
        var page = XDocument.Load(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages",
            "Workout", "WorkoutPage.xaml"));

        // The collection's EmptyView never drew inside this header-bearing list, so a filter that
        // matched nothing left a blank page under the chips. The card sits in the reading flow,
        // where the first workout would be.
        Assert.DoesNotContain(page.Descendants(), element => element.Name.LocalName == "EmptyView");
        var emptyCard = page.Descendants().Single(element =>
            (string?)element.Attribute("IsVisible") == "{Binding ShowEmptyWorkouts}");
        Assert.Equal("Border", emptyCard.Name.LocalName);

        // The copy is catalog keys, not English built from the localized chip label: both
        // languages read a sentence the catalog owns.
        var viewModel = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels",
            "Workout", "WorkoutViewModel.cs"));
        Assert.DoesNotContain("$\"No {", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Try another filter or show the full week.\"", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Large_text_cards_constrain_labels_instead_of_squeezing_them()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");

        var cycleHero = XDocument.Load(Path.Combine(uiRoot, "Controls", "Cycle", "CycleHeroCard.xaml"));
        var cycleLabel = cycleHero.Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{loc:LocalizedText Cycle_Hero_HeaderTitle}");
        Assert.DoesNotContain(cycleLabel.Ancestors(), ancestor => ancestor.Name.LocalName == "HorizontalStackLayout");
        var cycleHeader = cycleLabel.Ancestors().First(ancestor => ancestor.Name.LocalName == "Grid");
        Assert.Equal("Auto,*", cycleHeader.Attribute("ColumnDefinitions")?.Value);

        var activityCard = XDocument.Load(Path.Combine(uiRoot, "Controls", "Workout", "WorkoutActivityCard.xaml"));
        var activityTitle = activityCard.Descendants().Single(element =>
            element.Name.LocalName == "Label" &&
            (string?)element.Attribute("Text") == "{Binding Title, Converter={StaticResource LocalizedStringConverter}}");
        var activityRow = activityTitle.Ancestors()
            .FirstOrDefault(ancestor => ancestor.Name.LocalName == "FlexLayout");
        Assert.NotNull(activityRow);
        Assert.Equal("Wrap", activityRow!.Attribute("Wrap")?.Value);

        var profileDetails = XDocument.Load(Path.Combine(uiRoot, "Controls", "Profile", "ProfileDetailsContent.xaml"));
        var privacyLabel = profileDetails.Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{loc:LocalizedText Profile_Details_PreviewHiddenForPrivacy}");
        var privacyRow = privacyLabel.Ancestors()
            .FirstOrDefault(ancestor => ancestor.Name.LocalName == "FlexLayout");
        Assert.NotNull(privacyRow);
        Assert.Equal("Wrap", privacyRow!.Attribute("Wrap")?.Value);

        var vaultTitle = profileDetails.Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{loc:LocalizedText Profile_PhotoJourney_CardTitle}");
        var vaultRow = vaultTitle.Ancestors()
            .FirstOrDefault(ancestor => ancestor.Name.LocalName == "FlexLayout");
        Assert.NotNull(vaultRow);
        Assert.Equal("Wrap", vaultRow!.Attribute("Wrap")?.Value);
        Assert.Contains(profileDetails.Descendants(), element =>
            (string?)element.Attribute("Text") == "{loc:LocalizedText Profile_Details_StoredLocallyOnThisDevice}");
    }

    [Fact]
    public void Apple_sign_in_stays_user_initiated_on_the_login_screen()
    {
        var loginCodeBehind = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Auth",
            "LoginPage.xaml.cs"));

        Assert.DoesNotContain("TryStartAutomaticAppleSignIn", loginCodeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("SignInWithPlatformCommand", loginCodeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void Populated_schedule_strip_has_a_measured_wrapping_container()
    {
        var workoutPage = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Workout",
            "WorkoutPage.xaml"));
        var scheduleStart = workoutPage.IndexOf("BindableLayout.ItemsSource=\"{Binding ScheduleDays}\"",
            StringComparison.Ordinal);
        Assert.True(scheduleStart >= 0, "The schedule strip must bind ScheduleDays.");

        var scheduleContainerStart = workoutPage.LastIndexOf("<FlexLayout", scheduleStart, StringComparison.Ordinal);
        Assert.True(scheduleContainerStart >= 0, "The schedule strip must use a measured FlexLayout.");
        var scheduleEnd = workoutPage.IndexOf("</Border>", scheduleStart, StringComparison.Ordinal);
        Assert.True(scheduleEnd > scheduleStart, "The schedule strip must live inside its card.");
        var scheduleMarkup = workoutPage[scheduleContainerStart..scheduleEnd];

        Assert.Contains("Wrap=\"Wrap\"", scheduleMarkup, StringComparison.Ordinal);
        Assert.Contains("MinimumHeightRequest=\"{StaticResource PresentationMinimumTouchSize}\"",
            scheduleMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollView", scheduleMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void Schedule_day_cards_announce_day_date_workout_and_today()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var workoutPage = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Workout", "WorkoutPage.xaml"));
        Assert.Contains(
            "SemanticProperties.Description=\"{Binding AccessibilityDescription}\"",
            workoutPage,
            StringComparison.Ordinal);

        var scheduleDay = File.ReadAllText(Path.Combine(uiRoot, "Models", "UI", "Workout", "ScheduleDayItem.cs"));
        Assert.Contains("AccessibilityDescription", scheduleDay, StringComparison.Ordinal);

        var viewModel = File.ReadAllText(Path.Combine(uiRoot, "ViewModels", "Workout", "WorkoutViewModel.cs"));
        Assert.Contains("BuildScheduleDayAccessibility", viewModel, StringComparison.Ordinal);
        Assert.Contains("Workout_Page_ScheduleDayTodayAccessibility", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Weekday_preferences_use_compact_rows_not_a_card_per_day()
    {
        var preferencesPage = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Profile",
            "ProfileWorkoutPreferencesPage.xaml"));

        Assert.DoesNotContain("Style=\"{StaticResource CardSecondary}\"", preferencesPage, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ProfileMenuRow}\"", preferencesPage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SemanticProperties.Hint=\"{loc:LocalizedText Common_NotSelected}\"",
            preferencesPage,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Selectable_chips_use_the_shared_selection_surface_with_native_role_and_state()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var injury = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Injury", "InjuryLogContent.xaml"));
        Assert.Contains("<shared:SelectionCell", injury, StringComparison.Ordinal);
        Assert.Contains("IsSelected=\"{Binding IsSelected}\"", injury, StringComparison.Ordinal);
        Assert.Contains("MinimumHeightRequest=\"{StaticResource PresentationMinimumTouchSize}\"",
            injury, StringComparison.Ordinal);

        var selectionCell = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Shared", "SelectionCell.cs"));
        Assert.Contains("UIAccessibilityTrait.Button", selectionCell, StringComparison.Ordinal);
        Assert.Contains("UIAccessibilityTrait.Selected", selectionCell, StringComparison.Ordinal);

        var cycleDatePhase = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Cycle", "CycleDatePhaseModal.xaml"));
        Assert.Contains("sharedControls:SelectionCell", cycleDatePhase, StringComparison.Ordinal);

        var swap = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Workout", "WorkoutSwapPanel.xaml"));
        Assert.Contains("sharedControls:SelectionCell", swap, StringComparison.Ordinal);

        var preferences = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Workout",
            "WorkoutActivityPreferencePicker.xaml"));
        Assert.Contains("shared:SelectionCell", preferences, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_selection_cell_has_an_accessible_label()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var missing = Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path =>
            {
                var document = XDocument.Load(path);
                return document.Descendants()
                    .Where(element => element.Name.LocalName == "SelectionCell" &&
                                      element.Attribute("SemanticProperties.Description") is null)
                    .Select(_ => path);
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            "SelectionCell requires a direct SemanticProperties.Description: " + string.Join(", ", missing));
    }

    [Fact]
    public void Shared_styles_enforce_2_button_styles_2_card_styles_and_shared_surfaces()
    {
        var stylesPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Styles.xaml");
        Assert.True(File.Exists(stylesPath), $"Styles.xaml not found at {stylesPath}");

        var stylesContent = File.ReadAllText(stylesPath);

        // 1. Two brand button roles plus the platform-owned Sign in with Apple treatment.
        Assert.Contains("x:Key=\"ButtonPrimary\"", stylesContent);
        Assert.Contains("x:Key=\"ButtonSecondary\"", stylesContent);
        Assert.Contains("x:Key=\"ButtonApple\"", stylesContent);
        Assert.Equal(3, Regex.Matches(stylesContent, "<Style[^>]*TargetType=\"Button\"").Count);

        // 2. One picker/search surface and one editor surface; a second alias would be a duplicate style.
        Assert.Contains("x:Key=\"InputContainer\"", stylesContent);
        Assert.Contains("x:Key=\"PickerContainer\"", stylesContent);
        Assert.Contains("x:Key=\"EditorContainer\"", stylesContent);
        Assert.DoesNotContain("x:Key=\"SearchContainer\"", stylesContent);

        // 3. Two canonical card styles; selection is a state of the permitted card, not a third design.
        Assert.Contains("x:Key=\"CardPrimary\"", stylesContent);
        Assert.Contains("x:Key=\"CardSecondary\"", stylesContent);
        Assert.Equal(2, Regex.Matches(stylesContent, "<Style x:Key=\"(?:CardPrimary|CardSecondary)\"").Count);
        Assert.DoesNotContain("x:Key=\"CardAccent\"", stylesContent);
        Assert.DoesNotContain("x:Key=\"SelectableCard\"", stylesContent);
        Assert.DoesNotContain("BasedOn=\"{StaticResource CardAccent}\"", stylesContent);
        Assert.DoesNotContain("BasedOn=\"{StaticResource SelectableCard}\"", stylesContent);
        Assert.True(Regex.Matches(stylesContent, "<Style x:Key=\"ModalSurface\"").Count == 1);
        Assert.Single(Regex.Matches(stylesContent, "<Style x:Key=\"PickerContainer\"").Cast<Match>());
        Assert.Contains("x:Key=\"SelectionRadioRing\"", stylesContent);
        Assert.Contains("x:Key=\"SelectionCheckbox\"", stylesContent);

        var iconButtonFamilyStyles = Regex.Matches(
                stylesContent,
                "<Style x:Key=\"(?<key>[A-Za-z0-9_]*IconButton)\"")
            .Select(match => match.Groups["key"].Value)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "HeaderIconButton", "ModalIconButton" }, iconButtonFamilyStyles);

        // 4. The budget is checked by family, not just by the two known keys: a third card shipped
        //    under a Card-shaped name would otherwise pass while adding a visitable third design.
        var cardFamilyStyles = Regex.Matches(
                stylesContent,
                "<Style x:Key=\"(?<key>[A-Za-z0-9_]*Card[A-Za-z0-9_]*)\" TargetType=\"Border\"")
            .Select(match => match.Groups["key"].Value)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "CardPrimary", "CardSecondary" }, cardFamilyStyles);
    }

    [Fact]
    public void Soft_minimal_palette_meets_approved_contrast_floors()
    {
        var colorsPath = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Resources", "Styles", "Colors.xaml");
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(File.ReadAllText(colorsPath),
                     "<Color x:Key=\"(?<key>[^\"]+)\">(?<value>#[0-9A-Fa-f]{6})</Color>"))
        {
            colors[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        // Normal text keeps a 4.5:1 floor against every surface it is actually composed on.
        (string Foreground, string Background)[] normalTextPairs =
        [
            ("TextPrimary", "PageBackground"),
            ("TextSecondary", "PageBackground"),
            ("TextSecondary", "CardSurface"),
            ("TextSecondary", "CardSurfaceSecondary"),
            ("ActionPrimaryLight", "CardSurfaceSecondary"),
            ("ActionSecondaryLight", "CardSurfaceSecondary"),
            ("SuccessLight", "CardSurfaceSecondary"),
            ("WarningAccentLight", "CardSurfaceSecondary"),
            ("ErrorLight", "CardSurfaceSecondary"),
            ("TextPrimaryDark", "PageBackgroundDark"),
            ("TextSecondaryDark", "PageBackgroundDark"),
            ("TextSecondaryDark", "CardSurfaceSecondaryDark"),
            ("SuccessDark", "CardSurfaceSecondaryDark"),
            ("TextPrimary", "ActionSurfaceDark")
        ];

        foreach (var (foreground, background) in normalTextPairs)
        {
            var ratio = ContrastRatio(colors[foreground], colors[background]);
            Assert.True(ratio >= 4.5,
                $"{foreground} on {background} measures {ratio:F2}:1, below the 4.5:1 normal-text floor.");
        }

        // Essential non-text boundaries and the primary action fill clear their own floors.
        (string Foreground, string Background, double Minimum)[] nonTextPairs =
        [
            ("ControlBoundaryLight", "CardSurfaceSecondary", 3.0),
            ("ControlBoundaryDark", "CardSurfaceSecondaryDark", 3.0),
            ("ControlBoundaryLight", "PageBackground", 3.0),
            ("White", "ActionPrimaryLight", 4.5)
        ];

        foreach (var (foreground, background, minimum) in nonTextPairs)
        {
            var ratio = ContrastRatio(colors[foreground], colors[background]);
            Assert.True(ratio >= minimum,
                $"{foreground} on {background} measures {ratio:F2}:1, below the {minimum:F1}:1 floor.");
        }
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var value = hex.TrimStart('#');
        return (0.2126 * LinearChannel(value, 0))
               + (0.7152 * LinearChannel(value, 2))
               + (0.0722 * LinearChannel(value, 4));
    }

    private static double LinearChannel(string hex, int offset)
    {
        var channel = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255d;
        return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
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
            "{StaticResource EditorContainer}"
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
            "EditorContainer"
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
        var styleRegex = new Regex(@"\bStyle\s*=\s*""\{StaticResource Button(?:Primary|Secondary|Apple)\}""", RegexOptions.Compiled);
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
            "{StaticResource CardPrimary}", "{StaticResource CardSecondary}"
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

    [Fact]
    public void Workout_pages_include_informative_visuals_not_only_text()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(uiRoot, Path.Combine(parts)));

        var overview = Read("Pages", "Workout", "WorkoutPage.xaml");
        Assert.Contains("PageHeader.TitleIcon", overview, StringComparison.Ordinal);
        Assert.Contains("WorkoutHeaderVisual", overview, StringComparison.Ordinal);
        Assert.Contains("WorkoutNoPlanVisual", overview, StringComparison.Ordinal);

        var activityCard = Read("Controls", "Workout", "WorkoutActivityCard.xaml");
        Assert.Contains("IconGlyph", activityCard, StringComparison.Ordinal);
        Assert.Contains("StringToFluentIconImageSourceConverter", activityCard, StringComparison.Ordinal);

        var session = Read("Pages", "Workout", "WorkoutSessionPage.xaml");
        Assert.Contains("WorkoutSessionActivityVisual", session, StringComparison.Ordinal);
        Assert.Contains("WorkoutSessionExerciseVisual", session, StringComparison.Ordinal);
        Assert.Contains("SelectedActivity.IconGlyph", session, StringComparison.Ordinal);
        Assert.Contains("CurrentExercise.IconGlyph", session, StringComparison.Ordinal);

        var history = Read("Pages", "Workout", "WorkoutHistoryPage.xaml");
        Assert.Contains("WorkoutHistoryHeaderVisual", history, StringComparison.Ordinal);
        Assert.Contains("WorkoutHistoryEmptyVisual", history, StringComparison.Ordinal);
        Assert.Contains("WorkoutHistoryDateTile", history, StringComparison.Ordinal);

        var plan = Read("Pages", "Workout", "StrengthPlanGenerationPage.xaml");
        Assert.Contains("WorkoutPlanHeaderVisual", plan, StringComparison.Ordinal);
        Assert.Contains("BarHeight", plan, StringComparison.Ordinal);
        Assert.Contains("DayIconGlyph", plan, StringComparison.Ordinal);

        var planModels = Read("ViewModels", "Workout", "StrengthPlanGenerationDisplayModels.cs");
        Assert.Contains("WeatherMoon24", planModels, StringComparison.Ordinal);
        Assert.Contains("IsRestDay", planModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_form_input_exposes_an_accessible_name()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var inputNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Entry", "Editor", "Picker", "DatePicker", "TimePicker", "SearchBar",
            "CheckBox", "Switch", "Stepper", "Slider"
        };
        var missing = new List<string>();

        foreach (var path in Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var document = XDocument.Load(path);
            foreach (var element in document.Descendants())
            {
                if (!inputNames.Contains(element.Name.LocalName)) continue;
                if (!string.IsNullOrWhiteSpace((string?)element.Attribute("SemanticProperties.Description")))
                    continue;

                var binding = (string?)element.Attribute("Text") ??
                              (string?)element.Attribute("SelectedItem") ??
                              (string?)element.Attribute("Placeholder") ??
                              string.Empty;
                missing.Add($"{Path.GetRelativePath(RepoRoot, path)}: <{element.Name.LocalName} {binding}>".Trim());
            }
        }

        Assert.True(missing.Count == 0,
            "Every form input needs a VoiceOver name. Missing:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Section_titles_are_headings_with_explicit_decorative_exceptions()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var styles = File.ReadAllText(Path.Combine(uiRoot, "Resources", "Styles", "Styles.xaml"));
        Assert.Contains("Property=\"SemanticProperties.HeadingLevel\" Value=\"Level2\"", styles,
            StringComparison.Ordinal);

        var warning = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Cycle", "CycleWarningPopup.xaml"));
        Assert.Contains("SemanticProperties.HeadingLevel=\"None\"", warning, StringComparison.Ordinal);

        var phaseDetail = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Cycle", "CyclePhaseDetailPage.xaml"));
        Assert.Contains("SemanticProperties.HeadingLevel=\"None\"", phaseDetail, StringComparison.Ordinal);

        var calendar = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Cycle", "CycleCalendarCard.xaml"));
        Assert.Contains("SemanticProperties.HeadingLevel=\"None\"", calendar, StringComparison.Ordinal);
    }

    [Fact]
    public void Accessibility_display_titles_are_capped_and_wired_to_page_and_profile_headers()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var styles = File.ReadAllText(Path.Combine(uiRoot, "Resources", "Styles", "Styles.xaml"));
        Assert.Contains("x:Key=\"PresentationAccessibilityTitleMaxFontSize\">40<", styles, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"PresentationAccessibilityProfileNameMaxFontSize\"", styles, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"PresentationAccessibilitySectionTitleMaxFontSize\">36<", styles, StringComparison.Ordinal);
        Assert.Contains("sharedControls:CappedTextSize.IsSectionHeading", styles, StringComparison.Ordinal);

        var cappedText = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Shared", "CappedTextSize.cs"));
        Assert.Contains("AttachSectionTitle", cappedText, StringComparison.Ordinal);
        Assert.Contains("IsSectionHeadingProperty", cappedText, StringComparison.Ordinal);

        var pageHeader = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Shared", "PageHeader.cs"));
        Assert.Contains("CappedTextSize.Attach", pageHeader, StringComparison.Ordinal);

        var profilePage = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Profile", "ProfilePage.xaml"));
        Assert.Contains("x:Name=\"ProfileNameLabel\"", profilePage, StringComparison.Ordinal);

        var profileCodeBehind = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Profile", "ProfilePage.xaml.cs"));
        Assert.Contains("CappedTextSize.Attach", profileCodeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void Cycle_hero_card_does_not_collapse_its_quick_log_button_into_a_generic_container()
    {
        var hero = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Cycle", "CycleHeroCard.xaml"));
        Assert.DoesNotContain("Cycle_Hero_CardAccessibility", hero, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenQuickLogCommand}\"", hero, StringComparison.Ordinal);
        Assert.Contains("SemanticProperties.HeadingLevel=\"Level2\"", hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Privacy_page_title_fits_one_russian_word_at_accessibility_sizes()
    {
        var russian = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Services", "Localization", "RussianUiText.NutritionProfileAuth.cs"));
        Assert.Contains("Values[\"Profile_Privacy_Title\"] = \"Приватность\"", russian, StringComparison.Ordinal);
        Assert.Contains("Values[\"Profile_Privacy_Policy\"] = \"Приватность\"", russian, StringComparison.Ordinal);
        Assert.Contains("Values[\"Profile_Details_UnitsDisplay\"] = \"Единицы\"", russian, StringComparison.Ordinal);
        Assert.Contains("Values[\"Profile_Details_WorkoutAvailability\"] = \"Дни тренировок\"", russian, StringComparison.Ordinal);
        Assert.Contains("Values[\"Profile_Details_PersonalInfo\"] = \"Личные данные\"", russian, StringComparison.Ordinal);
        Assert.Contains("Values[\"Nutrition_FoodLog_CaloriesRemaining\"] = \"Осталось {0}\"", russian, StringComparison.Ordinal);
        var foodLogViewModel = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "ViewModels", "Nutrition", "FoodLogViewModel.cs"));
        Assert.Contains("Nutrition_FoodLog_CaloriesOver", foodLogViewModel, StringComparison.Ordinal);

        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var phaseExplorer = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Cycle", "CyclePhaseExplorerCarousel.xaml"));
        Assert.DoesNotContain("SemanticProperties.Hint", phaseExplorer, StringComparison.Ordinal);
        Assert.Contains("sharedControls:CardTapTarget Grid.ColumnSpan=\"2\"", phaseExplorer, StringComparison.Ordinal);
        Assert.Contains("CappedTextSize.IsPhasePill", phaseExplorer, StringComparison.Ordinal);

        var phaseSummary = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Cycle", "CyclePhaseSummarySheet.xaml"));
        Assert.Contains("OpenSelectedPhaseDetailsCommand", phaseSummary, StringComparison.Ordinal);

        var workoutPage = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Workout", "WorkoutPage.xaml"));
        Assert.Contains("Command=\"{Binding OpenHistoryCommand}\"", workoutPage, StringComparison.Ordinal);

        var dashboardNutrition = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Dashboard", "DashboardNutritionCard.xaml"));
        Assert.Contains("<sharedControls:CardTapTarget Command=\"{Binding OpenNutritionCommand}\"", dashboardNutrition, StringComparison.Ordinal);
        Assert.Contains("FlexLayout Direction=\"Row\" Wrap=\"Wrap\"", dashboardNutrition, StringComparison.Ordinal);
        Assert.DoesNotContain("<Border.GestureRecognizers>", dashboardNutrition, StringComparison.Ordinal);
        var workoutRussian = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Services", "Localization", "RussianUiText.CycleWorkout.cs"));
        Assert.Contains("Values[\"Workout_Schedule_DayTuesday\"] = \"ВТ\"", workoutRussian, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_preview_sheet_keeps_its_footer_on_screen_at_accessibility_sizes()
    {
        var code = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Pages", "Workout", "StrengthPlanGenerationPage.xaml.cs"));
        Assert.Contains("PreviewSheet.HeightRequest = sheetHeight", code, StringComparison.Ordinal);

        var russian = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Services", "Localization", "RussianUiText.WorkoutGeneration.cs"));
        Assert.Contains("Values[\"Workout_Generation_PreviewTitle\"] = \"Предпросмотр плана\"", russian, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_headers_drop_the_subtitle_at_accessibility_text_sizes()
    {
        var header = File.ReadAllText(Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Shared", "PageHeader.cs"));
        Assert.Equal(2, Regex.Matches(header, "!IsAccessibilityTextSize\\(\\)").Count);
    }

    [Fact]
    public void Card_choices_and_tappable_cards_expose_button_semantics()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var target = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Shared", "CardTapTarget.cs"));
        Assert.Contains("class CardTapTarget : Button", target, StringComparison.Ordinal);

        var units = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Profile", "ProfileUnitsDisplayPage.xaml"));
        Assert.Equal(2, Regex.Matches(units, "<sharedControls:CardTapTarget\\s").Count);
        Assert.Contains("<sharedControls:AdaptiveRow Grid.Row=\"2\" Columns=\"2\"", units, StringComparison.Ordinal);
        Assert.DoesNotContain("<TapGestureRecognizer Command=\"{Binding SelectImperialUnitsCommand}\"", units, StringComparison.Ordinal);
        Assert.DoesNotContain("<TapGestureRecognizer Command=\"{Binding SelectMetricUnitsCommand}\"", units, StringComparison.Ordinal);

        var nutritionSettings = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Profile", "ProfileNutritionSettingsPage.xaml"));
        Assert.Equal(4, Regex.Matches(nutritionSettings, "<sharedControls:CardTapTarget\\s").Count);
        Assert.Equal(4, Regex.Matches(nutritionSettings, "<sharedControls:AdaptiveRow").Count);

        var profileDetails = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Profile", "ProfileDetailsContent.xaml"));
        Assert.Contains("sharedControls:CardTapTarget", profileDetails, StringComparison.Ordinal);
        Assert.Contains("SemanticProperties.Description=\"{loc:LocalizedText Profile_PhotoJourney_Title}\"", profileDetails, StringComparison.Ordinal);
        Assert.Contains("SemanticProperties.Hint=\"{loc:LocalizedText Profile_Details_TapToOpenFullPhotoJourney}\"", profileDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("<TapGestureRecognizer Command=\"{Binding OpenVaultCommand}\"", profileDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void Decorative_icons_yield_width_at_accessibility_text_sizes()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "MuscleCuties.UI");
        var decoration = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Shared", "AccessibilityDecoration.cs"));
        Assert.Contains("HideAtAccessibilitySizeProperty", decoration, StringComparison.Ordinal);
        Assert.Contains("UIContentSizeCategoryDidChangeNotification", decoration, StringComparison.Ordinal);

        var dashboard = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Dashboard", "DashboardWorkoutCard.xaml"));
        Assert.Contains("AccessibilityDecoration.HideAtAccessibilitySize=\"True\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HeaderGrid\"", dashboard, StringComparison.Ordinal);

        var dashboardCode = File.ReadAllText(Path.Combine(uiRoot, "Controls", "Dashboard", "DashboardWorkoutCard.xaml.cs"));
        Assert.Contains("HeaderGrid.ColumnDefinitions[0].Width", dashboardCode, StringComparison.Ordinal);

        var profile = File.ReadAllText(Path.Combine(uiRoot, "Pages", "Profile", "ProfilePage.xaml"));
        Assert.Equal(3, Regex.Matches(profile, "AccessibilityDecoration.HideAtAccessibilitySize=\"True\"").Count);
    }

    [Fact]
    public void Nutrition_phase_focus_gives_long_copy_the_full_card_width()
    {
        var path = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Nutrition",
            "NutritionPhaseFocusCard.xaml");
        var document = XDocument.Load(path);
        var title = document.Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{Binding PhaseFocusTitle, Converter={StaticResource LocalizedStringConverter}}");
        var copy = document.Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{Binding PhaseFocusCopy, Converter={StaticResource LocalizedStringConverter}}");

        Assert.Equal("2", (string?)title.Attribute("Grid.ColumnSpan"));
        Assert.Equal("2", (string?)copy.Attribute("Grid.ColumnSpan"));
    }

    [Fact]
    public void Profile_menu_rows_expose_an_accessible_button_label_and_hint()
    {
        var path = Path.Combine(RepoRoot, "src", "MuscleCuties.UI", "Controls", "Profile",
            "ProfileDetailsContent.xaml");
        var document = XDocument.Load(path);
        var rows = document.Descendants()
            .Where(element => element.Name.LocalName == "Border" &&
                              (string?)element.Attribute("Style") == "{StaticResource ProfileMenuRow}" &&
                              element.Elements().Any(child => child.Name.LocalName == "Border.GestureRecognizers"))
            .ToList();

        Assert.Equal(6, rows.Count);
        foreach (var row in rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Attribute("SemanticProperties.Description")?.Value),
                "Every tappable profile row needs a VoiceOver label that includes its current value.");
            Assert.False(string.IsNullOrWhiteSpace(row.Attribute("SemanticProperties.Hint")?.Value),
                "Every tappable profile row needs a hint so iOS exposes it as a button to VoiceOver.");
        }
    }

    // Effective grid placement: an omitted Grid.Row/Grid.Column means row 0 / column 0, so the
    // guard keeps asserting behaviour when the XAML leaves a default value implicit.
    private static int EffectiveGridRow(XElement element) =>
        int.TryParse(element.Attribute("Grid.Row")?.Value, out var row) ? row : 0;

    private static int EffectiveGridColumn(XElement element) =>
        int.TryParse(element.Attribute("Grid.Column")?.Value, out var column) ? column : 0;

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
