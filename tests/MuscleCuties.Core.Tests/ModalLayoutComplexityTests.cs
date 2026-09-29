using System.Xml.Linq;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Static analysis guard for modal XAML layout complexity.
/// Tracks layout depth and element count against the current modal baselines.
/// These structural limits are a regression signal, not a runtime timing measurement.
/// </summary>
public class ModalLayoutComplexityTests
{
    private static readonly string ModalRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MuscleCuties.App", "Controls"));

    /// <summary>Max nesting depth of layout containers, including modal chrome and templates.</summary>
    private const int MaxNestingDepth = 9;

    /// <summary>Max total visual-tree elements in a single modal XAML file.
    /// Baseline: 135 covers the largest modal (MealSuggestionModal) with multi-state views.</summary>
    private const int MaxElementCount = 135;

    private static readonly HashSet<string> LayoutContainers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Grid", "StackLayout", "VerticalStackLayout", "HorizontalStackLayout",
        "FlexLayout", "AbsoluteLayout", "ScrollView"
    };

    public static TheoryData<string> ModalFiles()
    {
        var data = new TheoryData<string>();
        if (!Directory.Exists(ModalRoot))
            return data;

        foreach (var file in Directory.EnumerateFiles(ModalRoot, "*Modal*.xaml", SearchOption.AllDirectories))
            data.Add(Path.GetRelativePath(ModalRoot, file));

        return data;
    }

    [Theory]
    [MemberData(nameof(ModalFiles))]
    public void Modal_nesting_depth_stays_within_threshold(string relativePath)
    {
        var fullPath = Path.Combine(ModalRoot, relativePath);
        var doc = XDocument.Load(fullPath);
        var maxDepth = MeasureMaxLayoutDepth(doc.Root!, 0);

        Assert.True(maxDepth <= MaxNestingDepth,
            $"{relativePath}: layout nesting depth {maxDepth} exceeds threshold {MaxNestingDepth}");
    }

    [Theory]
    [MemberData(nameof(ModalFiles))]
    public void Modal_element_count_stays_within_threshold(string relativePath)
    {
        var fullPath = Path.Combine(ModalRoot, relativePath);
        var doc = XDocument.Load(fullPath);
        var count = CountElements(doc.Root!);

        Assert.True(count <= MaxElementCount,
            $"{relativePath}: element count {count} exceeds threshold {MaxElementCount}");
    }

    private static int MeasureMaxLayoutDepth(XElement element, int currentDepth)
    {
        var localName = element.Name.LocalName;
        var isLayout = LayoutContainers.Contains(localName);
        var depth = isLayout ? currentDepth + 1 : currentDepth;

        var max = depth;
        foreach (var child in element.Elements())
        {
            var childMax = MeasureMaxLayoutDepth(child, depth);
            if (childMax > max)
                max = childMax;
        }

        return max;
    }

    private static int CountElements(XElement element)
    {
        var count = 1;
        foreach (var child in element.Elements())
            count += CountElements(child);
        return count;
    }
}
