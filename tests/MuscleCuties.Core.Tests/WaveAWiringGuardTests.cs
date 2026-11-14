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
}
