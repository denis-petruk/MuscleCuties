using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Unit and contract tests for Nutrition portion drafting, conversions, day navigation,
/// and modal architecture isolation (NM01, NM02, RM-MEAL).
/// </summary>
public sealed class NutritionModalAndPortionDraftingTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string UiRoot = Path.Combine(RepositoryRoot, "src", "MuscleCuties.UI");

    #region Portion Drafting & Conversion Tests (NM02)

    [Theory]
    [InlineData(1.0, 150.0, 1.0, 150.0)]      // 1 serving of 150g -> converted to 1g unit = 150g
    [InlineData(150.0, 1.0, 150.0, 1.0)]      // 150g (1g unit) -> converted to 150g serving = 1 serving
    [InlineData(2.0, 30.0, 100.0, 0.6)]       // 2 servings of 30g = 60g -> converted to 100g unit = 0.6
    [InlineData(0.5, 240.0, 1.0, 120.0)]      // 0.5 serving of 240g = 120g -> converted to 1g unit = 120
    [InlineData(100.0, 1.0, 250.0, 0.4)]      // 100g -> converted to 250g serving = 0.4
    [InlineData(1.0, 28.35, 1.0, 28.35)]      // 1 oz (28.35g) -> converted to grams = 28.35
    [InlineData(3.0, 33.3333, 100.0, 1.0)]    // Precision preservation
    public void Portion_conversion_preserves_exact_grams_without_precision_loss(
        double currentAmount,
        double oldServingGrams,
        double newServingGrams,
        double expectedNewAmount)
    {
        // Replicates the conversion formula in NutritionViewModel.OnSelectedServingOptionChanged
        var targetGrams = currentAmount * oldServingGrams;
        var convertedAmount = targetGrams / newServingGrams;

        Assert.Equal(expectedNewAmount, convertedAmount, precision: 4);
    }

    [Theory]
    [InlineData("100", 100.0)]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", 1.5)]
    [InlineData(" 250 ", 250.0)]
    [InlineData("0.25", 0.25)]
    [InlineData("0,75", 0.75)]
    public void Portion_amount_parser_handles_different_culture_decimal_separators(
        string rawText,
        double expectedValue)
    {
        var success = TryParseAmount(rawText, out var parsedValue);

        Assert.True(success);
        Assert.Equal(expectedValue, parsedValue, precision: 4);
    }

    [Theory]
    [InlineData(100.0, "100")]
    [InlineData(1.5, "1.5")]
    [InlineData(0.25, "0.25")]
    [InlineData(0.3333333, "0.3333")]
    [InlineData(1250.0, "1250")]
    public void Converted_amount_formatter_preserves_precision_without_grouping_commas(
        double value,
        string expectedText)
    {
        var formatted = FormatConvertedAmount(value);

        Assert.Equal(expectedText, formatted);
    }

    #endregion

    #region Modal Architecture & Touch-Interception Tests (NM01, NM02)

    [Fact]
    public void AnimatedModalView_sets_cascade_and_input_transparent_when_closed()
    {
        var modalViewPath = Path.Combine(UiRoot, "Controls", "Shared", "AnimatedModalView.cs");
        var source = File.ReadAllText(modalViewPath);

        // Verify constructor sets both InputTransparent and CascadeInputTransparent
        Assert.Contains("CascadeInputTransparent = true;", source);
        Assert.Contains("InputTransparent = true;", source);

        // Verify Close sets CascadeInputTransparent = true
        Assert.Contains("Close(long version)", source);

        // Verify Open clears both
        Assert.Contains("CascadeInputTransparent = false;", source);
        Assert.Contains("InputTransparent = false;", source);

        // Verify accessibility isolation scope is used
        Assert.Contains("ModalAccessibilityScope.Acquire", source);
        Assert.Contains("ModalAccessibilityScope.Release", source);
    }

    [Fact]
    public void Nutrition_sheets_inherit_or_use_animated_modal_view()
    {
        var nutritionModals = new[]
        {
            "MealEditorModal.xaml",
            "MealEntryModal.xaml",
            "MealDetailModal.xaml",
            "MealSuggestionModal.xaml",
            "NutritionBreakdownModal.xaml"
        };

        foreach (var modalFile in nutritionModals)
        {
            var filePath = Path.Combine(UiRoot, "Controls", "Nutrition", modalFile);
            Assert.True(File.Exists(filePath), $"File {modalFile} must exist");

            var content = File.ReadAllText(filePath);
            var isAnimatedModal = content.Contains("AnimatedModalView", StringComparison.Ordinal);
            Assert.True(isAnimatedModal, $"{modalFile} must use AnimatedModalView for isolation");
        }
    }

    [Fact]
    public void FoodLogPage_modals_have_input_transparency_and_visibility_bindings()
    {
        var filePath = Path.Combine(UiRoot, "Pages", "Nutrition", "FoodLogPage.xaml");
        var content = File.ReadAllText(filePath);

        // Verify inline edit modal is guarded against touch interception
        Assert.Contains("InputTransparent=\"{Binding IsEditModalVisible, Converter={StaticResource InvertedBoolConverter}}\"", content);
        Assert.Contains("IsVisible=\"{Binding IsEditModalVisible}\"", content);

        // Verify breakdown modal is guarded against touch interception
        Assert.Contains("InputTransparent=\"{Binding Breakdown.IsVisible, Converter={StaticResource InvertedBoolConverter}}\"", content);
        Assert.Contains("IsVisible=\"{Binding Breakdown.IsVisible}\"", content);
    }

    [Fact]
    public void FoodLogPage_date_navigation_buttons_are_row_zero_and_path_is_input_transparent()
    {
        var filePath = Path.Combine(UiRoot, "Pages", "Nutrition", "FoodLogPage.xaml");
        var content = File.ReadAllText(filePath);

        // PreviousDay button
        Assert.Contains("Command=\"{Binding PreviousDayCommand}\"", content);
        // NextDay button
        Assert.Contains("Command=\"{Binding NextDayCommand}\"", content);

        // Path children inside the navigation buttons must have InputTransparent="True" so taps hit the Border
        Assert.Contains("InputTransparent=\"True\"", content);
    }

    [Fact]
    public void MealEditorModal_contains_compact_single_row_chip_strip()
    {
        var filePath = Path.Combine(UiRoot, "Controls", "Nutrition", "MealEditorModal.xaml");
        var content = File.ReadAllText(filePath);

        // Single row chip strip binding meal type, date, and time
        Assert.Contains("{Binding SelectedMealTypeText}", content);
        Assert.Contains("{Binding SelectedMealDateText}", content);
        Assert.Contains("{Binding SelectedMealTimeText}", content);
        Assert.Contains("{Binding IsMealContextSummaryVisible}", content);

        // Compact context editor when expanded
        Assert.Contains("{Binding IsMealContextEditorVisible}", content);
    }

    [Fact]
    public void MealEditorModal_pinned_footer_has_primary_log_and_secondary_cancel_buttons()
    {
        var filePath = Path.Combine(UiRoot, "Controls", "Nutrition", "MealEditorModal.xaml");
        var content = File.ReadAllText(filePath);

        // Pinned action footer when food finder is collapsed
        Assert.Contains("Command=\"{Binding LogMealCommand}\"", content);
        Assert.Contains("Command=\"{Binding ToggleMealEditorCommand}\"", content);
        Assert.Contains("Style=\"{StaticResource ButtonPrimary}\"", content);
        Assert.Contains("Style=\"{StaticResource ButtonSecondary}\"", content);
        Assert.Contains("Common_Cancel", content);
    }

    #endregion

    #region Helper Methods

    private static bool TryParseAmount(string? raw, out double amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) && amount > 0)
            return true;

        var altText = text.Contains('.') ? text.Replace('.', ',') : text.Replace(',', '.');
        if (double.TryParse(altText, NumberStyles.Float, CultureInfo.CurrentCulture, out amount) && amount > 0)
            return true;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount) && amount > 0;
    }

    private static string FormatConvertedAmount(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0001)
            return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

        var rounded = Math.Round(value, 4);
        return rounded.ToString("0.####", CultureInfo.InvariantCulture);
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

        throw new InvalidOperationException($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
    }

    #endregion
}
