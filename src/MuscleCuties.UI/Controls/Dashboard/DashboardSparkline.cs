using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Models.UI.Dashboard;

namespace MuscleCuties.UI.Controls.Dashboard;

public sealed class DashboardSparkline : GraphicsView, IDrawable
{
    public static readonly BindableProperty PointsProperty = BindableProperty.Create(
        nameof(Points), typeof(IReadOnlyList<DashboardTrendPoint>), typeof(DashboardSparkline),
        Array.Empty<DashboardTrendPoint>(), propertyChanged: (view, _, _) => ((DashboardSparkline)view).Invalidate());

    public static readonly BindableProperty IsWeightProperty = BindableProperty.Create(
        nameof(IsWeight), typeof(bool), typeof(DashboardSparkline), false,
        propertyChanged: (view, _, _) => ((DashboardSparkline)view).Invalidate());

    public DashboardSparkline()
    {
        Drawable = this;
        HeightRequest = 68;
        InputTransparent = true;
    }

    public IReadOnlyList<DashboardTrendPoint> Points
    {
        get => (IReadOnlyList<DashboardTrendPoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public bool IsWeight
    {
        get => (bool)GetValue(IsWeightProperty);
        set => SetValue(IsWeightProperty, value);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var points = Points;
        if (points is null || points.Count == 0 || dirtyRect.Width < 20 || dirtyRect.Height < 20)
            return;

        var values = points.Select(point => IsWeight ? point.WeightKg : point.ReadinessScore).ToArray();
        var present = values.Where(value => value.HasValue && double.IsFinite(value.Value)).Select(value => value!.Value).ToArray();
        if (present.Length == 0)
            return;

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var ink = IsWeight ? Color.FromArgb("#C45A86") : Color.FromArgb("#5586B5");
        canvas.StrokeColor = isDark ? Color.FromArgb("#505060") : Color.FromArgb("#DDDDE3");
        canvas.StrokeSize = 1;
        canvas.DrawLine(8, dirtyRect.Height - 8, dirtyRect.Width - 8, dirtyRect.Height - 8);

        var min = IsWeight ? present.Min() : 0d;
        var max = IsWeight ? present.Max() : 100d;
        var padding = IsWeight ? Math.Max(0.5d, (max - min) * 0.25d) : 0d;
        min -= padding;
        max += padding;
        var range = Math.Max(0.01d, max - min);
        if (!double.IsFinite(range) || range <= 0.0001d)
            return;

        var left = 8f;
        var width = dirtyRect.Width - 16f;
        var top = 8f;
        var height = dirtyRect.Height - 18f;

        canvas.StrokeColor = ink;
        canvas.StrokeSize = 2.5f;
        var lastPointIndex = Math.Max(values.Length - 1, 1);
        for (var i = 1; i < values.Length; i++)
        {
            if (!values[i - 1].HasValue || !values[i].HasValue ||
                !double.IsFinite(values[i - 1]!.Value) || !double.IsFinite(values[i]!.Value))
                continue;
            canvas.DrawLine(left + width * (i - 1) / lastPointIndex,
                top + height * (float)(1d - (values[i - 1]!.Value - min) / range),
                left + width * i / lastPointIndex,
                top + height * (float)(1d - (values[i]!.Value - min) / range));
        }

        canvas.FillColor = ink;
        for (var i = 0; i < values.Length; i++)
        {
            if (!values[i].HasValue || !double.IsFinite(values[i]!.Value))
                continue;
            var x = left + width * i / lastPointIndex;
            var y = top + height * (float)(1d - (values[i]!.Value - min) / range);
            canvas.FillCircle(x, y, 3.5f);
        }
    }
}
