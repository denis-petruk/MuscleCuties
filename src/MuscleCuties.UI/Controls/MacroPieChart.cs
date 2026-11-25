
namespace MuscleCuties.UI.Controls;

/// <summary>A pie of the day's macros: each slice is a share of the total, drawn from the values bound to it.</summary>
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
        AppThemeResources.GetRequired<Color>("DashboardChartTrackLight"),
        propertyChanged: Redraw);

    public static readonly BindableProperty ProteinColorProperty = BindableProperty.Create(
        nameof(ProteinColor),
        typeof(Color),
        typeof(MacroPieChart),
        AppThemeResources.GetRequired<Color>("ProteinLight"),
        propertyChanged: Redraw);

    public static readonly BindableProperty CarbsColorProperty = BindableProperty.Create(
        nameof(CarbsColor),
        typeof(Color),
        typeof(MacroPieChart),
        AppThemeResources.GetRequired<Color>("CarbsLight"),
        propertyChanged: Redraw);

    public static readonly BindableProperty FatsColorProperty = BindableProperty.Create(
        nameof(FatsColor),
        typeof(Color),
        typeof(MacroPieChart),
        AppThemeResources.GetRequired<Color>("FatsLight"),
        propertyChanged: Redraw);

    public static readonly BindableProperty InnerColorProperty = BindableProperty.Create(
        nameof(InnerColor),
        typeof(Color),
        typeof(MacroPieChart),
        default,
        propertyChanged: Redraw);

    public static readonly BindableProperty InnerStrokeColorProperty = BindableProperty.Create(
        nameof(InnerStrokeColor),
        typeof(Color),
        typeof(MacroPieChart),
        default,
        propertyChanged: Redraw);

    /// <summary>Makes the chart its own drawing surface and redraws on every value change.</summary>
    public MacroPieChart()
    {
        Drawable = this;
        HeightRequest = AppThemeResources.GetDouble("UiDimension132");
        WidthRequest = AppThemeResources.GetDouble("UiDimension132");
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

    /// <summary>Draws one slice per macro, skipping a zero share so an empty macro leaves no sliver, and draws nothing at all</summary>
    /// when every share is zero.</summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var values = new[]
        {
            (Value: NonNegativeFinite(ProteinValue), Color: ProteinColor),
            (Value: NonNegativeFinite(CarbsValue), Color: CarbsColor),
            (Value: NonNegativeFinite(FatsValue), Color: FatsColor)
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
        canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth2");
        canvas.DrawEllipse(x + inset, y + inset, diameter - inset * 2f, diameter - inset * 2f);
    }

    /// <summary>The wedge for one slice between two angles, closed back to the centre so it fills as a slice rather than an</summary>
    /// arc.</summary>
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

    /// <summary>The point on the chart's circle at an angle in degrees, where zero points up.</summary>
    private static PointF PointOnCircle(float centerX, float centerY, float radius, float angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new PointF(
            centerX + radius * (float)Math.Cos(radians),
            centerY + radius * (float)Math.Sin(radians));
    }

    /// <summary>A value safe to draw with: negative and non-finite input become zero, so bad data cannot break the path.</summary>
    private static float NonNegativeFinite(float value)
    {
        return float.IsFinite(value) && value > 0f ? value : 0f;
    }

    /// <summary>The change handler every bindable property shares: redraw when a drawn value actually changed.</summary>
    private static void Redraw(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MacroPieChart chart)
            chart.Invalidate();
    }
}
