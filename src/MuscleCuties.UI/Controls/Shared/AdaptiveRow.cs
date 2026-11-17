using Microsoft.Maui.Controls;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// A row of equal columns that stacks when the reader's text size is an accessibility size. Used for
/// metric tiles, whose labels are single long Russian words ("Восстановление", "Продолжительность",
/// "Интенсивность"), and for two-button sheet footers, whose primary label is a whole phrase
/// ("Использовать фото"). Neither can sit in a half or a third of the card at those sizes without
/// breaking inside a word, and the text may not be shrunk below the scale the reader chose.
/// </summary>
public sealed class AdaptiveRow : Grid
{
    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns),
        typeof(int),
        typeof(AdaptiveRow),
        2,
        propertyChanged: static (bindable, _, _) => ((AdaptiveRow)bindable).ApplyLayout(force: true));

    private int _appliedColumns;
    private int _appliedChildCount;

    public AdaptiveRow()
    {
        ColumnSpacing = AppThemeResources.GetDouble("UiSpaceTight");
        RowSpacing = AppThemeResources.GetDouble("UiSpaceTight");
    }

    /// <summary>How many items share a row at the standard text sizes.</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        // Text size can change while the page is alive, so the column count is re-checked on every
        // measure instead of only when the control is built.
        ApplyLayout(force: false);
        return base.MeasureOverride(widthConstraint, heightConstraint);
    }

    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);
        ApplyLayout(force: false);
    }

    protected override void OnChildRemoved(Element child, int oldLogicalIndex)
    {
        base.OnChildRemoved(child, oldLogicalIndex);
        ApplyLayout(force: false);
    }

    private void ApplyLayout(bool force)
    {
        var columns = Math.Max(1, IsAccessibilityTextSize() ? 1 : Columns);
        var childCount = Children.Count;
        // Placement has to be redone for every child: the first measure after a child is added would
        // otherwise hand it row 0 / column 0 and stack every later tile on top of the first.
        if (!force && columns == _appliedColumns && childCount == _appliedChildCount && RowDefinitions.Count > 0)
            return;

        _appliedColumns = columns;
        _appliedChildCount = childCount;
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();
        for (var column = 0; column < columns; column++)
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var index = 0;
        foreach (var child in Children)
        {
            if (child is not BindableObject bindable)
                continue;

            var row = index / columns;
            if (row >= RowDefinitions.Count)
                RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            SetRow(bindable, row);
            SetColumn(bindable, index % columns);
            index++;
        }
    }

    private static bool IsAccessibilityTextSize()
    {
#if IOS
        var category = UIKit.UIApplication.SharedApplication.PreferredContentSizeCategory?.ToString();
        return category is not null && category.Contains("Accessibility", StringComparison.Ordinal);
#else
        return false;
#endif
    }
}
