using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

namespace MuscleCuties.App.Controls.Shared;

public class PhaseVisualView : ContentView
{
    private const double FrameIntervalMilliseconds = 33;
    private const float FullTurn = MathF.PI * 2f;

    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(PhaseVisualView),
        string.Empty,
        propertyChanged: OnSourceChanged);

    private readonly Image _staticImage;
    private readonly GraphicsView _animationView;
    private readonly PhaseAnimationDrawable _drawable = new();
    private IDispatcherTimer? _timer;
    private DateTime _animationStartedAt;

    public PhaseVisualView()
    {
        InputTransparent = true;

        _staticImage = new Image
        {
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true
        };

        _animationView = new GraphicsView
        {
            Drawable = _drawable,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true,
            IsVisible = false
        };

        Content = new Grid
        {
            Children =
            {
                _staticImage,
                _animationView
            }
        };
    }

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdateVisualSource();
    }

    private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PhaseVisualView view)
            view.UpdateVisualSource();
    }

    private void UpdateVisualSource()
    {
        var source = Source?.Trim() ?? string.Empty;
        var shouldAnimate = IsAnimatedSource(source);

        _drawable.Source = source;
        _staticImage.Source = shouldAnimate || string.IsNullOrWhiteSpace(source)
            ? null
            : ImageSource.FromFile(source);
        _staticImage.IsVisible = !shouldAnimate && !string.IsNullOrWhiteSpace(source);
        _animationView.IsVisible = shouldAnimate;

        if (shouldAnimate && Handler is not null)
            StartAnimation();
        else
            StopAnimation();

        _animationView.Invalidate();
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
        if (!_animationView.IsVisible || Handler is null)
        {
            StopAnimation();
            return;
        }

        var elapsed = (DateTime.UtcNow - _animationStartedAt).TotalMilliseconds;
        _drawable.Progress = (float)(elapsed % 1800d / 1800d);
        _animationView.Invalidate();
    }

    private static bool IsAnimatedSource(string source) =>
        source.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private sealed class PhaseAnimationDrawable : IDrawable
    {
        public string Source { get; set; } = string.Empty;
        public float Progress { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
                return;

            canvas.Antialias = true;

            var side = Math.Min(dirtyRect.Width, dirtyRect.Height);
            var scale = side / 100f;
            var offsetX = dirtyRect.X + (dirtyRect.Width - side) / 2f;
            var offsetY = dirtyRect.Y + (dirtyRect.Height - side) / 2f;

            canvas.SaveState();
            canvas.Translate(offsetX, offsetY);
            canvas.Scale(scale, scale);

            if (Source.Contains("menstrual", StringComparison.OrdinalIgnoreCase))
                DrawBloodDrops(canvas, Progress);
            else if (Source.Contains("ovulatory", StringComparison.OrdinalIgnoreCase))
                DrawSun(canvas, Progress);
            else if (Source.Contains("luteal", StringComparison.OrdinalIgnoreCase))
                DrawMoon(canvas, Progress);
            else
                DrawPlant(canvas, Progress);

            canvas.RestoreState();
        }

        private static void DrawBloodDrops(ICanvas canvas, float progress)
        {
            var wave = MathF.Sin(progress * FullTurn);

            DrawDrop(
                canvas,
                new PointF(50f, 24f + wave * 3f),
                28f,
                Color.FromArgb("#F7A6B0"),
                Color.FromArgb("#8F3A46"));

            DrawDrop(
                canvas,
                new PointF(28f, 48f - wave * 2f),
                16f,
                Color.FromArgb("#F3B7BE"),
                Color.FromArgb("#9A4550"));

            DrawDrop(
                canvas,
                new PointF(72f, 51f + wave * 2.5f),
                17f,
                Color.FromArgb("#E88996"),
                Color.FromArgb("#8F3A46"));
        }

        private static void DrawSun(ICanvas canvas, float progress)
        {
            var pulse = 0.5f + MathF.Sin(progress * FullTurn) * 0.5f;
            var center = new PointF(50f, 50f);
            var rayStart = 29f + pulse * 1.5f;
            var rayEnd = 43f + pulse * 4f;
            var seamlessRotation = progress * FullTurn / 12f;

            canvas.StrokeColor = Color.FromArgb("#C99414");
            canvas.StrokeSize = 5f;
            canvas.StrokeLineCap = LineCap.Round;

            for (var index = 0; index < 12; index++)
            {
                var angle = index / 12f * FullTurn + seamlessRotation;
                var start = PointOnCircle(center, rayStart, angle);
                var end = PointOnCircle(center, rayEnd, angle);
                canvas.DrawLine(start.X, start.Y, end.X, end.Y);
            }

            canvas.FillColor = Color.FromArgb("#FFE7A3");
            canvas.FillCircle(center.X, center.Y, 23f + pulse * 1.8f);

            canvas.StrokeColor = Color.FromArgb("#8D6B00");
            canvas.StrokeSize = 3f;
            canvas.DrawCircle(center.X, center.Y, 21f + pulse * 1.3f);

            canvas.FillColor = Color.FromArgb("#FFF5D2");
            canvas.FillCircle(43f, 42f, 5f + pulse);
        }

        private static void DrawPlant(ICanvas canvas, float progress)
        {
            var grow = 0.82f + MathF.Sin(progress * FullTurn) * 0.08f;
            var sway = MathF.Sin(progress * FullTurn) * 2.4f;
            var stemTop = 76f - 43f * grow;

            canvas.FillColor = Color.FromArgb("#334C8F51");
            canvas.FillEllipse(28f, 78f, 44f, 10f);

            canvas.StrokeColor = Color.FromArgb("#3F7D43");
            canvas.StrokeSize = 5f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(50f, 80f, 50f + sway, stemTop);

            DrawLeaf(
                canvas,
                new PointF(50f, 62f),
                new PointF(22f - sway, 47f),
                17f,
                Color.FromArgb("#BDE8BF"),
                Color.FromArgb("#3F7D43"));

            DrawLeaf(
                canvas,
                new PointF(50f, 54f),
                new PointF(77f + sway, 37f),
                16f,
                Color.FromArgb("#D7F4D8"),
                Color.FromArgb("#3F7D43"));

            DrawLeaf(
                canvas,
                new PointF(50f + sway, stemTop + 3f),
                new PointF(50f + sway, 18f),
                13f,
                Color.FromArgb("#E8F8E8"),
                Color.FromArgb("#3F7D43"));

            canvas.FillColor = Color.FromArgb("#C85A87");
            canvas.FillCircle(50f + sway, stemTop + 4f, 3f);
        }

        private static void DrawMoon(ICanvas canvas, float progress)
        {
            var glow = 0.5f + MathF.Sin(progress * FullTurn) * 0.5f;
            var bob = MathF.Sin(progress * FullTurn) * 2f;
            var breathe = 1f + glow * 0.035f;

            canvas.SaveState();
            canvas.Translate(50f, 50f + bob);
            canvas.Scale(breathe, breathe);
            canvas.Translate(-50f, -50f);

            var moon = new PathF();
            moon.MoveTo(68f, 14f);
            moon.CurveTo(45f, 15f, 27f, 33f, 27f, 55f);
            moon.CurveTo(27f, 78f, 51f, 91f, 74f, 80f);
            moon.CurveTo(58f, 77f, 46f, 65f, 46f, 51f);
            moon.CurveTo(46f, 35f, 55f, 21f, 68f, 14f);
            moon.Close();

            canvas.FillColor = Color.FromArgb("#EFE4FF");
            canvas.FillPath(moon);

            canvas.StrokeColor = Color.FromArgb("#745398");
            canvas.StrokeSize = 3f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(moon);

            canvas.StrokeColor = Color.FromArgb("#B799D6");
            canvas.StrokeSize = 1.6f;
            canvas.DrawArc(45f, 21f, 28f, 56f, 101f, 257f, false, false);

            canvas.RestoreState();

            canvas.FillColor = Color.FromArgb("#C85A87");
            canvas.FillCircle(68f, 25f + bob * 0.5f, 2f + glow * 0.7f);
            canvas.FillCircle(78f, 40f - bob * 0.4f, 1.7f + glow * 0.6f);
            canvas.FillCircle(68f, 65f + bob * 0.35f, 1.8f + glow * 0.5f);

            DrawStar(canvas, 28f, 25f, 4.8f + glow);
            DrawStar(canvas, 78f, 72f, 4f + glow * 0.8f);
        }

        private static void DrawDrop(ICanvas canvas, PointF top, float size, Color fill, Color stroke)
        {
            var halfWidth = size * 0.44f;
            var bottom = top.Y + size * 1.28f;
            var centerX = top.X;

            var drop = new PathF();
            drop.MoveTo(centerX, top.Y);
            drop.CurveTo(centerX - halfWidth, top.Y + size * 0.42f, centerX - halfWidth, top.Y + size, centerX, bottom);
            drop.CurveTo(centerX + halfWidth, top.Y + size, centerX + halfWidth, top.Y + size * 0.42f, centerX, top.Y);
            drop.Close();

            canvas.FillColor = fill;
            canvas.FillPath(drop);

            canvas.StrokeColor = stroke;
            canvas.StrokeSize = MathF.Max(1.7f, size * 0.09f);
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(drop);

            canvas.FillColor = Color.FromArgb("#55FFFFFF");
            canvas.FillEllipse(centerX - halfWidth * 0.36f, top.Y + size * 0.35f, halfWidth * 0.26f, size * 0.26f);
        }

        private static void DrawStar(ICanvas canvas, float centerX, float centerY, float size)
        {
            canvas.StrokeColor = Color.FromArgb("#C85A87");
            canvas.StrokeSize = 2f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(centerX - size, centerY, centerX + size, centerY);
            canvas.DrawLine(centerX, centerY - size, centerX, centerY + size);
        }

        private static void DrawLeaf(ICanvas canvas, PointF root, PointF tip, float width, Color fill, Color stroke)
        {
            var dx = tip.X - root.X;
            var dy = tip.Y - root.Y;
            var length = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
            var normalX = -dy / length * width;
            var normalY = dx / length * width;

            var leftControl = new PointF(root.X + dx * 0.42f + normalX, root.Y + dy * 0.42f + normalY);
            var rightControl = new PointF(root.X + dx * 0.42f - normalX, root.Y + dy * 0.42f - normalY);

            var path = new PathF();
            path.MoveTo(root.X, root.Y);
            path.CurveTo(leftControl.X, leftControl.Y, tip.X - normalX * 0.12f, tip.Y - normalY * 0.12f, tip.X, tip.Y);
            path.CurveTo(tip.X + normalX * 0.12f, tip.Y + normalY * 0.12f, rightControl.X, rightControl.Y, root.X, root.Y);
            path.Close();

            canvas.FillColor = fill;
            canvas.FillPath(path);
            canvas.StrokeColor = stroke;
            canvas.StrokeSize = 2f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
        }

        private static PointF PointOnCircle(PointF center, float radius, float angleRadians) =>
            new(
                center.X + radius * MathF.Cos(angleRadians),
                center.Y + radius * MathF.Sin(angleRadians));
    }
}
