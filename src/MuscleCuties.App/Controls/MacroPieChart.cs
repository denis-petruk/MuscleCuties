using Microsoft.Maui.Graphics;

namespace MuscleCuties.App.Controls;

public class MacroPieChart : GraphicsView, IDrawable
{
    private const float FullCircleDegrees = 360f;
    private const float StartAngleDegrees = -90f;
    private const float FullSliceThresholdDegrees = 359.5f;
    private const float SliceStepDegrees = 3f;

    public static readonly BindableProperty ProteinValueProperty = BindableProperty.Create(
        nameof(ProteinValue),
        typeof(float),
        typeof(MacroPieChart),
        0f,
        propertyChanged: Redraw);

    public static readonly BindableProperty CarbsValueProperty = BindableProperty.Create(
        nameof(CarbsValue),
        typeof(float),
        typeof(MacroPieChart),
        0f,
        propertyChanged: Redraw);

    public static readonly BindableProperty FatsValueProperty = BindableProperty.Create(
        nameof(FatsValue),
        typeof(float),
        typeof(MacroPieChart),
        0f,
        propertyChanged: Redraw);

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor),
        typeof(Color),
        typeof(MacroPieChart),
        Color.FromArgb("#F0D8E2"),
        propertyChanged: Redraw);

    public static readonly BindableProperty ProteinColorProperty = BindableProperty.Create(
        nameof(ProteinColor),
        typeof(Color),
        typeof(MacroPieChart),
        Color.FromArgb("#A65AC8"),
        propertyChanged: Redraw);

    public static readonly BindableProperty CarbsColorProperty = BindableProperty.Create(
        nameof(CarbsColor),
        typeof(Color),
        typeof(MacroPieChart),
        Color.FromArgb("#E3A13B"),
        propertyChanged: Redraw);

    public static readonly BindableProperty FatsColorProperty = BindableProperty.Create(
        nameof(FatsColor),
        typeof(Color),
        typeof(MacroPieChart),
        Color.FromArgb("#6F8E4E"),
        propertyChanged: Redraw);

    public static readonly BindableProperty InnerColorProperty = BindableProperty.Create(
        nameof(InnerColor),
        typeof(Color),
        typeof(MacroPieChart),
        Color.FromArgb("#FFFFFF"),
        propertyChanged: Redraw);

    public static readonly BindableProperty InnerStrokeColorProperty = BindableProperty.Create(
        nameof(InnerStrokeColor),
        typeof(Color),
        typeof(MacroPieChart),
        Color.FromArgb("#F0D8E2"),
        propertyChanged: Redraw);

    public MacroPieChart()
    {
        Drawable = this;
        HeightRequest = 132;
        WidthRequest = 132;
    }

    public float ProteinValue
    {
        get => (float)GetValue(ProteinValueProperty);
        set => SetValue(ProteinValueProperty, value);
    }

    public float CarbsValue
    {
        get => (float)GetValue(CarbsValueProperty);
        set => SetValue(CarbsValueProperty, value);
    }

    public float FatsValue
    {
        get => (float)GetValue(FatsValueProperty);
        set => SetValue(FatsValueProperty, value);
    }

    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    public Color ProteinColor
    {
        get => (Color)GetValue(ProteinColorProperty);
        set => SetValue(ProteinColorProperty, value);
    }

    public Color CarbsColor
    {
        get => (Color)GetValue(CarbsColorProperty);
        set => SetValue(CarbsColorProperty, value);
    }

    public Color FatsColor
    {
        get => (Color)GetValue(FatsColorProperty);
        set => SetValue(FatsColorProperty, value);
    }

    public Color InnerColor
    {
        get => (Color)GetValue(InnerColorProperty);
        set => SetValue(InnerColorProperty, value);
    }

    public Color InnerStrokeColor
    {
        get => (Color)GetValue(InnerStrokeColorProperty);
        set => SetValue(InnerStrokeColorProperty, value);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var values = new[]
        {
            (Value: Math.Max(0f, ProteinValue), Color: ProteinColor),
            (Value: Math.Max(0f, CarbsValue), Color: CarbsColor),
            (Value: Math.Max(0f, FatsValue), Color: FatsColor)
        };

        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        var diameter = Math.Max(0f, size - 4f);
        var x = dirtyRect.Center.X - diameter / 2f;
        var y = dirtyRect.Center.Y - diameter / 2f;
        var total = values.Sum(item => item.Value);

        canvas.Antialias = true;
        canvas.FillColor = TrackColor;
        canvas.FillEllipse(x, y, diameter, diameter);

        if (total > 0f)
        {
            var centerX = dirtyRect.Center.X;
            var centerY = dirtyRect.Center.Y;
            var radius = diameter / 2f;
            var startAngle = StartAngleDegrees;

            foreach (var segment in values.Where(item => item.Value > 0f))
            {
                var sweep = segment.Value / total * FullCircleDegrees;
                canvas.FillColor = segment.Color;

                if (sweep >= FullSliceThresholdDegrees)
                    canvas.FillEllipse(x, y, diameter, diameter);
                else
                    canvas.FillPath(BuildSlicePath(centerX, centerY, radius, startAngle, sweep));

                startAngle += sweep;
            }
        }

        var inset = diameter * 0.28f;
        canvas.FillColor = InnerColor;
        canvas.FillEllipse(x + inset, y + inset, diameter - inset * 2f, diameter - inset * 2f);

        canvas.StrokeColor = InnerStrokeColor;
        canvas.StrokeSize = 2f;
        canvas.DrawEllipse(x + inset, y + inset, diameter - inset * 2f, diameter - inset * 2f);
    }

    private static PathF BuildSlicePath(float centerX, float centerY, float radius, float startAngle, float sweep)
    {
        var path = new PathF();
        path.MoveTo(centerX, centerY);

        var start = PointOnCircle(centerX, centerY, radius, startAngle);
        path.LineTo(start.X, start.Y);

        var steps = Math.Max(1, (int)Math.Ceiling(sweep / SliceStepDegrees));
        for (var step = 1; step <= steps; step++)
        {
            var angle = startAngle + sweep * step / steps;
            var point = PointOnCircle(centerX, centerY, radius, angle);
            path.LineTo(point.X, point.Y);
        }

        path.LineTo(centerX, centerY);
        path.Close();
        return path;
    }

    private static PointF PointOnCircle(float centerX, float centerY, float radius, float angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new PointF(
            centerX + radius * (float)Math.Cos(radians),
            centerY + radius * (float)Math.Sin(radians));
    }

    private static void Redraw(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MacroPieChart chart)
            chart.Invalidate();
    }
}
