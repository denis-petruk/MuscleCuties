using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class UiFullAuditEnhancementTests
{
    private static string RepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "MuscleCuties.sln")) ||
                Directory.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }

    private static string ReadUi(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "MuscleCuties.UI", Path.Combine(parts)));

    [Fact]
    public void WorkoutPage_header_closes_tab_bar_gap_and_day_strip_matches_calendar_language()
    {
        var workout = ReadUi("Pages", "Workout", "WorkoutPage.xaml");
        var headerMatch = Regex.Match(workout, @"<CollectionView\.Header>(?<header>[\s\S]*?)</CollectionView\.Header>");
        Assert.True(headerMatch.Success);
        var header = headerMatch.Groups["header"].Value;

        Assert.DoesNotContain("PresentationPageContentPadding", header, StringComparison.Ordinal);
        Assert.Contains("AlignItems=\"Start\"", header, StringComparison.Ordinal);
        Assert.Contains("HeightRequest=\"{StaticResource UiDimension10}\"", header, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource DayStripDayNumber}\"", header, StringComparison.Ordinal);
        Assert.Contains("<Ellipse", header, StringComparison.Ordinal);

        var card = ReadUi("Controls", "Workout", "WorkoutActivityCard.xaml");
        Assert.Contains("Grid.Row=\"0\" Grid.Column=\"1\"", card, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\" Grid.Column=\"0\" Grid.ColumnSpan=\"2\"", card, StringComparison.Ordinal);
        Assert.Matches(@"<FlexLayout\s+Grid\.Row=""2""[^>]*IsVisible=""\{Binding CanMarkOptional\}""", card);
    }

    [Fact]
    public void NutritionBalanceCard_uses_radial_rings_and_full_width_micronutrients_row()
    {
        var balance = ReadUi("Controls", "Nutrition", "NutritionBalanceCard.xaml");

        Assert.Contains("DashboardProgressRing", balance, StringComparison.Ordinal);
        Assert.Contains("AdaptiveRow", balance, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProgressBar", balance, StringComparison.Ordinal);
        Assert.DoesNotContain("MacroProgressGrid", balance, StringComparison.Ordinal);
        Assert.Contains("OpenMicronutrientsModalCommand", balance, StringComparison.Ordinal);
        Assert.Contains("ChevronRight24", balance, StringComparison.Ordinal);

        var nutrition = ReadUi("Pages", "Nutrition", "NutritionPage.xaml");
        var footerMatch = Regex.Match(nutrition, @"<CollectionView\.Footer>(?<footer>[\s\S]*?)</CollectionView\.Footer>");
        Assert.True(footerMatch.Success);
        Assert.Contains("UiDimension96", footerMatch.Groups["footer"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Cycle_food_log_and_injury_p0_layout_and_localization_defects_are_fixed()
    {
        var warning = XDocument.Parse(ReadUi("Controls", "Cycle", "CycleWarningPopup.xaml"));
        var scrollRow = warning.Descendants().First(e => e.Name.LocalName == "ScrollView")
            .Attributes().FirstOrDefault(a => a.Name.LocalName == "Grid.Row")?.Value;
        var footerStack = warning.Descendants()
            .First(e => e.Name.LocalName == "VerticalStackLayout" &&
                        e.Descendants().Any(c => c.Name.LocalName == "Button"));
        var footerRow = footerStack.Attributes().FirstOrDefault(a => a.Name.LocalName == "Grid.Row")?.Value;
        Assert.Equal("2", scrollRow);
        Assert.Equal("3", footerRow);

        var cycleCal = ReadUi("Controls", "Cycle", "CycleCalendarCard.xaml");
        Assert.Contains("Margin=\"{StaticResource UiInsets12_0}\"", cycleCal, StringComparison.Ordinal);

        var phaseDetail = ReadUi("Pages", "Cycle", "CyclePhaseDetailPage.xaml");
        var pageHeaderIdx = phaseDetail.IndexOf("<sharedControls:PageHeader", StringComparison.Ordinal);
        var scrollIdx = phaseDetail.IndexOf("<ScrollView", StringComparison.Ordinal);
        Assert.True(pageHeaderIdx > 0 && pageHeaderIdx < scrollIdx,
            "CyclePhaseDetailPage PageHeader must sit pinned above ScrollView.");
        Assert.DoesNotContain("SemanticProperties.HeadingLevel=\"Level1\"", phaseDetail, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(phaseDetail, @"Binding PhaseWindow\b"));

        var foodLog = ReadUi("Pages", "Nutrition", "FoodLogPage.xaml");
        Assert.Contains("Grid.Row=\"3\"\n                        IsVisible=\"{Binding IsManualEditorVisible}\"",
            foodLog.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource EditorContainer}\"", foodLog, StringComparison.Ordinal);
        var foodFooter = Regex.Match(foodLog, @"<CollectionView\.Footer>(?<footer>[\s\S]*?)</CollectionView\.Footer>");
        Assert.True(foodFooter.Success);
        Assert.Contains("UiDimension96", foodFooter.Groups["footer"].Value, StringComparison.Ordinal);

        var injury = ReadUi("Controls", "Injury", "InjuryLogContent.xaml");
        Assert.DoesNotContain("Value=\"Hide\"", injury, StringComparison.Ordinal);
    }

    [Fact]
    public void Cross_app_padding_localization_and_polish_fixes_hold()
    {
        var plan = ReadUi("Pages", "Workout", "StrengthPlanGenerationPage.xaml");
        var planHeaderIdx = plan.IndexOf("<sharedControls:PageHeader", StringComparison.Ordinal);
        var planScrollIdx = plan.IndexOf("<ScrollView", StringComparison.Ordinal);
        Assert.True(planHeaderIdx > 0 && planHeaderIdx < planScrollIdx,
            "StrengthPlanGenerationPage PageHeader must sit pinned above ScrollView.");
        Assert.DoesNotContain("PresentationPageContentPadding", plan, StringComparison.Ordinal);

        var quiz = ReadUi("Pages", "Onboarding", "QuizPage.xaml");
        Assert.DoesNotContain("PresentationPageContentPadding", quiz, StringComparison.Ordinal);

        var startup = ReadUi("Pages", "Startup", "AppStartupPage.xaml");
        Assert.DoesNotContain("PresentationPageContentPadding", startup, StringComparison.Ordinal);

        var vault = ReadUi("Pages", "Profile", "PhotoJourneyVaultPage.xaml");
        Assert.DoesNotContain("Margin=\"{DynamicResource PresentationPageContentPadding}\"", vault, StringComparison.Ordinal);
        Assert.Contains("PresentationCollectionHorizontalInset", vault, StringComparison.Ordinal);

        var privacy = ReadUi("Pages", "Profile", "ProfilePrivacyPage.xaml");
        Assert.Contains("PresentationPageContentPadding", privacy, StringComparison.Ordinal);

        var feedback = ReadUi("Pages", "Profile", "ProfileFeedbackPage.xaml");
        Assert.DoesNotContain("▾", feedback, StringComparison.Ordinal);

        var personalInfo = ReadUi("Pages", "Profile", "ProfilePersonalInfoPage.xaml");
        Assert.Contains("Style=\"{StaticResource StateBusyInline}\"", personalInfo, StringComparison.Ordinal);

        var recovery = ReadUi("Pages", "Auth", "PasswordRecoveryPage.xaml");
        var step2Start = recovery.IndexOf("IsStep2", StringComparison.Ordinal);
        var verifyIdx = recovery.IndexOf("Command=\"{Binding VerifyCodeCommand}\"", step2Start, StringComparison.Ordinal);
        var resendIdx = recovery.IndexOf("Command=\"{Binding ResendCodeCommand}\"", step2Start, StringComparison.Ordinal);
        Assert.True(verifyIdx > 0 && resendIdx > verifyIdx,
            "PasswordRecoveryPage Step 2 must place primary Verify Code above secondary Resend Code.");

        var session = ReadUi("Pages", "Workout", "WorkoutSessionPage.xaml");
        Assert.Contains("CurrentExercise.CuesText, Converter={StaticResource LocalizedStringConverter}", session, StringComparison.Ordinal);
        Assert.Contains("CurrentExercise.QuickTipsText, Converter={StaticResource LocalizedStringConverter}", session, StringComparison.Ordinal);

        var featured = ReadUi("Controls", "Workout", "FeaturedWorkoutCard.xaml");
        Assert.Contains("MovementNotes, Converter={StaticResource LocalizedStringConverter}", featured, StringComparison.Ordinal);

        var workoutPage = ReadUi("Pages", "Workout", "WorkoutPage.xaml");
        Assert.Contains("ReschedulingWorkoutTitle, Converter={StaticResource LocalizedStringConverter}", workoutPage, StringComparison.Ordinal);
        Assert.Contains("DisplayText, Converter={StaticResource LocalizedStringConverter}", workoutPage, StringComparison.Ordinal);

        var history = ReadUi("Pages", "Workout", "WorkoutHistoryPage.xaml");
        Assert.Contains("SemanticProperties.Description=\"{Binding DisplayName, Converter={StaticResource LocalizedStringConverter}}\"", history, StringComparison.Ordinal);
        Assert.Contains("HeightRequest=\"{StaticResource UiDimension96}\"", history, StringComparison.Ordinal);

        var swap = ReadUi("Controls", "Workout", "WorkoutSwapPanel.xaml");
        Assert.Contains("SemanticProperties.Description=\"{Binding Name, Converter={StaticResource LocalizedStringConverter}}\"", swap, StringComparison.Ordinal);

        var mealDetail = ReadUi("Controls", "Nutrition", "MealDetailModal.xaml");
        Assert.Contains("Text=\"{Binding Name, Converter={StaticResource LocalizedStringConverter}}\"", mealDetail, StringComparison.Ordinal);

        var suggestion = ReadUi("Controls", "Nutrition", "MealSuggestionModal.xaml");
        Assert.DoesNotContain("WidthRequest=\"{StaticResource UiDimension152}\"", suggestion, StringComparison.Ordinal);

        var dashWorkout = ReadUi("Controls", "Dashboard", "DashboardWorkoutCard.xaml");
        Assert.DoesNotContain("<Border>\n                            <Grid>", dashWorkout.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding ShowWorkoutSubtitle}\"", dashWorkout, StringComparison.Ordinal);

        var dashVm = ReadUi("ViewModels", "Dashboard", "DashboardViewModel.cs");
        Assert.Contains("public bool ShowWorkoutSubtitle", dashVm, StringComparison.Ordinal);

        var mealCard = ReadUi("Controls", "Nutrition", "MealCardView.xaml");
        Assert.Matches(@"x:Name=""EmptyMealRow""[\s\S]*?Padding=""\{StaticResource UiInsets16_8\}""", mealCard);
    }
}
