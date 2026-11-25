using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MuscleCuties.UI.Controls.Dashboard;

/// <summary>
///     Circular progress ring drawn for a dashboard target.
/// </summary>
public sealed class DashboardProgressRing : GraphicsView, IDrawable
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(float), typeof(DashboardProgressRing), 0f,
        propertyChanged: (view, _, _) => ((DashboardProgressRing)view).Invalidate());

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(DashboardProgressRing), AppThemeResources.GetColor("DashboardWeightAccent", default),
        propertyChanged: (view, _, _) => ((DashboardProgressRing)view).Invalidate());

    /// <summary>Wires the ring to redraw itself whenever the value it shows changes, rather than on every frame.</summary>
    public DashboardProgressRing()
    {
        Drawable = this;
        InputTransparent = true;
    }

    public float Progress
    {
        get => (float)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Color RingColor
    {
        get => (Color)GetValue(RingColorProperty);
        set => SetValue(RingColorProperty, value);
    }

    /// <summary>Draws the ring's track and its filled arc, clamped to a full circle so an overshoot cannot wrap past the</summary>
    /// start.</summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        var ringInset = AppThemeResources.GetDouble("UiDimension5");
        var diameterReduction = AppThemeResources.GetDouble("UiDimension10");
        if (size <= AppThemeResources.GetDouble("UiDimension14"))
            return;

        var x = (dirtyRect.Width - size) / 2f + (float)ringInset;
        var y = (dirtyRect.Height - size) / 2f + (float)ringInset;
        var diameter = size - (float)diameterReduction;
        canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth6");
        canvas.StrokeColor = AppThemeResources.GetColor(
            Application.Current?.RequestedTheme == AppTheme.Dark ? "DashboardRingTrackDark" : "DashboardRingTrackLight",
            default);
        canvas.DrawEllipse(x, y, diameter, diameter);

        var progress = float.IsFinite(Progress) ? Math.Clamp(Progress, 0f, 1f) : 0f;
        if (progress <= 0f)
            return;

        canvas.StrokeColor = RingColor;
        canvas.StrokeLineCap = LineCap.Round;
        if (progress >= 0.999f)
        {
            canvas.DrawEllipse(x, y, diameter, diameter);
            return;
        }

        // Angles are counter-clockwise from 3 o'clock. Start at the top (90) and sweep clockwise by progress * 360,
        // so the end angle is 90 - sweep. The previous end angle (90 + sweep) drew the complement (inverted fill).
        var endAngle = (90f - 360f * progress + 360f) % 360f;
        canvas.DrawArc(x, y, diameter, diameter, 90f, endAngle, true, false);
    }
}
