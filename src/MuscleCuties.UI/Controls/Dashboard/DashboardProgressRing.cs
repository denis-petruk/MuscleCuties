using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MuscleCuties.UI.Controls.Dashboard;

public sealed class DashboardProgressRing : GraphicsView, IDrawable
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(float), typeof(DashboardProgressRing), 0f,
        propertyChanged: (view, _, _) => ((DashboardProgressRing)view).Invalidate());

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(DashboardProgressRing), Color.FromArgb("#C45A86"),
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
        if (size <= 14)
            return;

        var x = (dirtyRect.Width - size) / 2f + 5f;
        var y = (dirtyRect.Height - size) / 2f + 5f;
        var diameter = size - 10f;
        canvas.StrokeSize = 6f;
        canvas.StrokeColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#42424B")
            : Color.FromArgb("#E9E5E8");
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
