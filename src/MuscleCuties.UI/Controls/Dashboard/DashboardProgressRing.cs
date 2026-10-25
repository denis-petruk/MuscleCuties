using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Dashboard;

public sealed class DashboardProgressRing : GraphicsView, IDrawable
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(float), typeof(DashboardProgressRing), 0f,
        propertyChanged: (view, _, _) => ((DashboardProgressRing)view).Invalidate());

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(DashboardProgressRing), AppThemeResources.GetColor("DashboardAccentPink", default),
        propertyChanged: (view, _, _) => ((DashboardProgressRing)view).Invalidate());

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

        canvas.DrawArc(x, y, diameter, diameter, 90f, 90f + 360f * progress, true, false);
    }
}
