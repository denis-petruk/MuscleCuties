using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace MuscleCuties.App.Controls.Shared;

public class FitnessIconView : GraphicsView, IDrawable
{
    private const double FrameIntervalMilliseconds = 33;
    private const float FullTurn = MathF.PI * 2f;
    private const float FireVisualScale = 1.5f;
    private IDispatcherTimer? _timer;
    private DateTime _animationStartedAt;
    private float _progress;

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon),
        typeof(string),
        typeof(FitnessIconView),
        "Fire",
        propertyChanged: Redraw);

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor),
        typeof(Color),
        typeof(FitnessIconView),
        Color.FromArgb("#C85A87"),
        propertyChanged: Redraw);

    public static readonly BindableProperty IsAnimatedProperty = BindableProperty.Create(
        nameof(IsAnimated),
        typeof(bool),
        typeof(FitnessIconView),
        false,
        propertyChanged: OnIsAnimatedChanged);

    public FitnessIconView()
    {
        Drawable = this;
        InputTransparent = true;
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
            StopAnimation();
        else if (IsAnimated)
            StartAnimation();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        canvas.Antialias = true;

        var side = Math.Min(dirtyRect.Width, dirtyRect.Height);
        var scale = side / 24f;
        var offsetX = dirtyRect.X + (dirtyRect.Width - side) / 2f;
        var offsetY = dirtyRect.Y + (dirtyRect.Height - side) / 2f;

        canvas.SaveState();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale, scale);

        if (Icon.Equals("Muscle", StringComparison.OrdinalIgnoreCase))
            DrawMuscle(canvas, IconColor, _progress);
        else
            DrawFire(canvas, IconColor, _progress);

        canvas.RestoreState();
    }

    private static void DrawFire(ICanvas canvas, Color color, float progress)
    {
        var flicker = MathF.Sin(progress * FullTurn) * 0.35f;

        canvas.SaveState();
        canvas.Translate(12f, 12f);
        canvas.Scale(FireVisualScale, FireVisualScale);
        canvas.Translate(-12f, -12f);

        canvas.StrokeColor = color;
        canvas.StrokeSize = 1.45f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        var flame = new PathF();
        flame.MoveTo(12f, 3.5f);
        flame.CurveTo(9.2f, 7f, 7f, 9.8f, 7f, 13.3f);
        flame.CurveTo(7f, 16.1f, 9.2f, 18.6f, 12f, 18.6f);
        flame.CurveTo(14.8f, 18.6f, 17f, 16.1f, 17f, 13.3f);
        flame.CurveTo(17f, 10.9f, 15.7f, 9.1f, 13.8f, 7.2f);
        flame.CurveTo(14f, 8.7f, 13.5f, 9.9f, 12.3f, 10.8f);
        flame.CurveTo(12.2f, 8.3f, 12f, 6f, 12f, 3.5f);
        flame.Close();
        canvas.DrawPath(flame);

        var inner = new PathF();
        inner.MoveTo(11f, 13.5f + flicker);
        inner.CurveTo(10.5f, 14.6f, 10.4f, 15.3f, 10.4f, 16.4f);
        inner.CurveTo(10.4f, 17.5f, 11.2f, 18.4f, 12.3f, 18.4f);
        inner.CurveTo(13.4f, 18.4f, 14.2f, 17.5f, 14.2f, 16.4f);
        inner.CurveTo(14.2f, 15.5f, 13.8f, 14.9f, 13f, 14.2f);
        inner.CurveTo(13f, 15f, 12.7f, 15.6f, 12.1f, 16f);
        inner.CurveTo(12f, 15.1f, 11.6f, 14.3f, 11f, 13.5f);
        canvas.DrawPath(inner);

        canvas.RestoreState();
    }

    private static void DrawMuscle(ICanvas canvas, Color color, float progress)
    {
        var flex = MathF.Sin(progress * FullTurn) * 0.45f;

        canvas.StrokeColor = color;
        canvas.StrokeSize = 1.6f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        var forearm = new PathF();
        forearm.MoveTo(6.75f, 18.25f);
        forearm.LineTo(4.9f, 18.25f);
        forearm.CurveTo(3.3f, 18.25f, 2f, 16.95f, 2f, 15.35f);
        forearm.LineTo(2f, 12.95f);
        forearm.CurveTo(2f, 11.4f, 3.25f, 10.15f, 4.8f, 10.15f);
        forearm.LineTo(6.05f, 10.15f);
        forearm.CurveTo(7.2f, 10.15f, 8.25f, 10.8f, 8.75f, 11.85f + flex);
        forearm.LineTo(9.4f, 13.2f);
        canvas.DrawPath(forearm);

        var bicep = new PathF();
        bicep.MoveTo(8.4f, 13.6f);
        bicep.CurveTo(9.25f, 9.45f, 11.8f, 6.15f, 15.55f, 4.4f);
        bicep.CurveTo(16.9f, 3.75f, 18.45f, 4.75f, 18.45f, 6.25f);
        bicep.CurveTo(18.45f, 7.2f, 17.85f, 8.05f, 16.95f, 8.4f);
        bicep.LineTo(15.25f, 9.05f);
        canvas.DrawPath(bicep);

        var shoulder = new PathF();
        shoulder.MoveTo(11.75f, 9.85f);
        shoulder.LineTo(15.9f, 9.85f);
        shoulder.CurveTo(19.25f, 9.85f, 22f, 12.6f, 22f, 15.95f);
        shoulder.CurveTo(22f, 18.75f, 19.75f, 21f, 16.95f, 21f);
        shoulder.LineTo(12.55f, 21f);
        shoulder.CurveTo(10.15f, 21f, 7.95f, 19.7f, 6.75f, 17.6f);
        canvas.DrawPath(shoulder);

        canvas.StrokeSize = 1.3f;
        canvas.DrawLine(8.3f, 10.45f, 8.3f, 18.15f);
        canvas.DrawArc(10.65f, 11.2f - flex, 10.1f, 5.4f + flex, 198f, 344f, false, false);
        canvas.DrawArc(12.7f, 14.4f, 5.2f, 3.6f, 204f, 342f, false, false);
    }

    private static void OnIsAnimatedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not FitnessIconView view)
            return;

        if ((bool)newValue && view.Handler is not null)
            view.StartAnimation();
        else
            view.StopAnimation();

        view.Invalidate();
    }

    private void StartAnimation()
    {
        _animationStartedAt = DateTime.UtcNow;

        if (_timer is null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(FrameIntervalMilliseconds);
            _timer.Tick += OnAnimationTick;
        }

        if (!_timer.IsRunning)
            _timer.Start();
    }

    private void StopAnimation()
    {
        if (_timer is { IsRunning: true })
            _timer.Stop();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!IsAnimated || Handler is null)
        {
            StopAnimation();
            return;
        }

        var elapsed = (DateTime.UtcNow - _animationStartedAt).TotalMilliseconds;
        _progress = (float)(elapsed % 1400d / 1400d);
        Invalidate();
    }

    private static void Redraw(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FitnessIconView view)
            view.Invalidate();
    }
}
