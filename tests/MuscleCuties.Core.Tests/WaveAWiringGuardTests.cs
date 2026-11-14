using System.Xml.Linq;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Wave A guards read the shipped markup and platform composition so a shared primitive cannot be
/// added in isolation: each test proves the screen that owns the defect actually consumes it.
/// </summary>
public sealed class WaveAWiringGuardTests
{
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";

    /// <summary>
    /// D2: the Nutrition tab drew no page title at all; its root grid kept an empty Auto row where
    /// the header belonged. The tab must use the same shared page header as the other top-level
    /// pages, with no bespoke hero title beside it.
    /// </summary>
    [Fact]
    public void Nutrition_page_uses_the_shared_page_header()
    {
        var document = Load("src", "MuscleCuties.UI", "Pages", "Nutrition", "NutritionPage.xaml");

        var header = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "PageHeader");
        Assert.NotNull(header);
        Assert.Equal("{loc:LocalizedText Nutrition_Page_Nutrition}", header!.Attribute("Title")?.Value);
        Assert.Equal("False", header.Attribute("ShowBack")?.Value);

        var root = document.Root!.Elements(Maui + "Grid").Single();
        Assert.Equal("Auto,*", root.Attribute("RowDefinitions")?.Value);
        var headerRow = header.Ancestors().Last(ancestor => ancestor.Name.LocalName == "Grid");
        Assert.Same(root, headerRow);

        var collection = root.Elements(Maui + "CollectionView").Single();
        Assert.Equal("1", collection.Attribute("Grid.Row")?.Value);
    }

    /// <summary>
    /// D22: the macro-targets card kept its title in a `*,Auto` grid beside "Дополнительные
    /// настройки". At accessibility text sizes the button took the width it wanted and the title
    /// collapsed into a four-line sliver. The card header must use the shared adaptive row so the
    /// title and the action stack when the text no longer fits side by side.
    /// </summary>
    [Fact]
    public void Macro_targets_header_stacks_beside_its_action_at_large_text()
    {
        var document = Load("src", "MuscleCuties.UI", "Pages", "Profile", "ProfileNutritionSettingsPage.xaml");

        var title = document.Descendants().Single(element =>
            element.Name.LocalName == "IconSectionHeader" &&
            ((string?)element.Attribute("Text"))?.Contains(
                "Profile_NutritionSettings_MacroTargets", StringComparison.Ordinal) == true);

        var row = title.Ancestors().FirstOrDefault(element => element.Name.LocalName == "AdaptiveRow");
        Assert.NotNull(row);
        Assert.Equal("2", row!.Attribute("Columns")?.Value);

        Assert.DoesNotContain(
            title.Ancestors().Where(element => element.Name.LocalName == "Grid"),
            grid => (string?)grid.Attribute("ColumnDefinitions") == "*,Auto");
    }

    /// <summary>
    /// D24/D48: both date and time pickers keep the native UITextField's rounded border inside the
    /// shared InputContainer, so the field drew two frames. They also each rendered their own
    /// TimePicker, and hydration's two copies fell back to the device's 12-hour pattern. One shared
    /// field must own the 24-hour format and the container, with no timer updating the value.
    /// </summary>
    [Fact]
    public void Date_and_time_values_use_one_24_hour_field_without_native_chrome()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI");
        var timeFieldPath = Path.Combine(uiRoot, "Controls", "Shared", "TimeField.xaml");
        Assert.True(File.Exists(timeFieldPath),
            "Date and time values must use the shared TimeField instead of per-screen TimePicker markup.");

        var timeField = File.ReadAllText(timeFieldPath);
        Assert.Contains("Format=\"HH:mm\"", timeField);
        Assert.Contains("{StaticResource InputContainer}", timeField);

        var strayPickers = SourceFiles(uiRoot, "*.xaml")
            .Where(path => File.ReadAllText(path).Contains("<TimePicker", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(uiRoot, path).Replace('\\', '/'))
            .ToArray();
        Assert.Equal(new[] { "Controls/Shared/TimeField.xaml" }, strayPickers);

        foreach (var relative in new[]
                 {
                     "Controls/Nutrition/MealEntryModal.xaml",
                     "Controls/Nutrition/MealEditorModal.xaml",
                     "Controls/Nutrition/HydrationLogView.xaml"
                 })
        {
            var screen = XDocument.Load(Path.Combine(uiRoot, relative));
            Assert.Contains(screen.Descendants(), element => element.Name.LocalName == "TimeField");
        }

        var composition = File.ReadAllText(Path.Combine(uiRoot, "Composition", "PlatformCompositionExtensions.cs"));
        var iosStart = composition.IndexOf("#elif IOS", StringComparison.Ordinal);
        Assert.True(iosStart >= 0, "The iOS picker chrome mappings must exist.");
        var dateStart = composition.IndexOf("DatePickerHandler.Mapper", iosStart, StringComparison.Ordinal);
        var timeStart = composition.IndexOf("TimePickerHandler.Mapper", dateStart, StringComparison.Ordinal);
        Assert.True(dateStart >= 0 && timeStart > dateStart, "The iOS picker chrome mappings must exist.");
        Assert.Contains("BorderStyle = UITextBorderStyle.None", composition[dateStart..timeStart]);
        Assert.Contains("BorderStyle = UITextBorderStyle.None", composition[timeStart..]);

        var nutritionViewModels = Path.Combine(uiRoot, "ViewModels", "Nutrition");
        foreach (var source in SourceFiles(nutritionViewModels, "*.cs"))
        {
            var text = File.ReadAllText(source);
            Assert.DoesNotContain("DispatcherTimer", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Device.StartTimer", text, StringComparison.Ordinal);
            Assert.DoesNotContain("PeriodicTimer", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// D36: every sheet drew its own title row, and the check-in/detail sheets still hand-built the
    /// close button beside a wrapping label. One shared header owns the title, muted subtitle and
    /// close affordance so those rows cannot drift apart again.
    /// </summary>
    [Fact]
    public void Sheets_use_the_shared_sheet_header()
    {
        var uiRoot = Path.Combine(RepositoryRoot(), "src", "MuscleCuties.UI", "Controls");
        var sheets = new[]
        {
            Path.Combine(uiRoot, "Nutrition", "MealEntryModal.xaml"),
            Path.Combine(uiRoot, "Nutrition", "MealEditorModal.xaml"),
            Path.Combine(uiRoot, "Nutrition", "NutritionBreakdownModal.xaml"),
            Path.Combine(uiRoot, "Dashboard", "DailyCheckInModal.xaml")
        };

        foreach (var path in sheets)
        {
            var document = XDocument.Load(path);
            var headers = document.Descendants()
                .Where(element => element.Name.LocalName == "ModalSheetHeader")
                .ToArray();
            Assert.True(headers.Length == 1,
                $"{Path.GetFileName(path)} must use exactly one ModalSheetHeader, found {headers.Length}.");
            Assert.DoesNotContain(document.Descendants(), element =>
                (string?)element.Attribute("Style") is "{StaticResource ModalTitle}" or "{StaticResource ModalSubtitle}");
        }
    }

    private static XDocument Load(params string[] parts) =>
        XDocument.Load(Path.Combine(RepositoryRoot(), Path.Combine(parts)));

    private static string RepositoryRoot()
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

    private static IEnumerable<string> SourceFiles(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
