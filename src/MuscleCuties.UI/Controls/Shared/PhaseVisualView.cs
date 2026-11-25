using SkiaSharp.Extended.UI.Controls;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Phase illustration drawn from the current cycle phase's palette and shape.
/// </summary>
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

    private readonly GraphicsView _animationView;
    private readonly PhaseAnimationDrawable _drawable = new();

    private readonly SKLottieView _lottieView;

    private readonly Image _staticImage;
    private DateTime _animationStartedAt;
    private IDispatcherTimer? _timer;

    /// <summary>Builds the still image and the drawn layer on top of it, and ignores touches because the control is only ever</summary>
    /// decoration.</summary>
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

        _lottieView = new SKLottieView
        {
            RepeatCount = -1,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true,
            IsVisible = false
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
                _lottieView,
                _animationView
            }
        };
    }

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Refreshes the visual once the view is attached.</summary>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdateVisualSource();
    }

    /// <summary>Refreshes the visual when its source changes.</summary>
    private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PhaseVisualView view)
            view.UpdateVisualSource();
    }

    /// <summary>Chooses what the control shows for a source: an animated drawing for the JSON artwork, the still image for</summary>
    /// anything else, and nothing at all for an empty source.</summary>
    private void UpdateVisualSource()
    {
        var source = Source?.Trim() ?? string.Empty;
        var shouldAnimate = IsAnimatedSource(source);

        _drawable.Source = source;
        _staticImage.Source = shouldAnimate || string.IsNullOrWhiteSpace(source)
            ? null
            : ImageSource.FromFile(source);
        _staticImage.IsVisible = !shouldAnimate && !string.IsNullOrWhiteSpace(source);

        if (shouldAnimate)
        {
            _lottieView.Source = new SKFileLottieImageSource { File = source };
            _lottieView.IsVisible = true;
            _animationView.IsVisible = false;
            StopAnimation();
        }
        else
        {
            _lottieView.IsVisible = false;
            _animationView.IsVisible = false;
            StopAnimation();
        }
    }

    /// <summary>Starts the animation clock from now and runs one timer for the whole control, so the phase artwork keeps a</summary>
    /// steady loop rather than restarting with each frame.</summary>
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

    /// <summary>Stops and unhooks the timer, so a hidden control costs nothing.</summary>
    private void StopAnimation()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnAnimationTick;
        _timer = null;
    }

    /// <summary>Advances the drawing by the elapsed time rather than a fixed step, and does nothing while the control is</summary>
    /// invisible or detached.</summary>
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

    /// <summary>The animated artwork is recognised by its file type, so a new phase needs no code to get one.</summary>
    private static bool IsAnimatedSource(string source)
    {
        return source.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Draws the phase artwork procedurally from its source name, so the loop animates without shipping a video.</summary>
    private sealed class PhaseAnimationDrawable : IDrawable
    {
        /// <summary>Which artwork the drawing renders.</summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>Where the loop is, from 0 to 1, set on each frame before the draw.</summary>
        public float Progress { get; set; }

        /// <summary>Draws the artwork for the source, and nothing at all for a zero-sized canvas so a collapsed control cannot</summary>
        /// throw.</summary>
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

        /// <summary>The menstrual artwork: slow drops whose sway is driven by the loop progress.</summary>
        private static void DrawBloodDrops(ICanvas canvas, float progress)
        {
            var wave = MathF.Sin(progress * FullTurn);

            DrawDrop(
                canvas,
                new PointF(50f, 24f + wave * 3f),
                28f,
                Palette("PhaseVisualMenstrualDropPrimary"),
                Palette("PhaseVisualMenstrualStrokePrimary"));

            DrawDrop(
                canvas,
                new PointF(28f, 48f - wave * 2f),
                16f,
                Palette("PhaseVisualMenstrualDropSecondary"),
                Palette("PhaseVisualMenstrualStrokeSecondary"));

            DrawDrop(
                canvas,
                new PointF(72f, 51f + wave * 2.5f),
                17f,
                Palette("PhaseVisualMenstrualDropTertiary"),
                Palette("PhaseVisualMenstrualStrokePrimary"));
        }

        /// <summary>The ovulation artwork: a pulsing highlight.</summary>
        private static void DrawSun(ICanvas canvas, float progress)
        {
            var pulse = 0.5f + MathF.Sin(progress * FullTurn) * 0.5f;
            var center = new PointF(50f, 50f);
            var rayStart = 29f + pulse * 1.5f;
            var rayEnd = 43f + pulse * 4f;
            var seamlessRotation = progress * FullTurn / 12f;

            canvas.StrokeColor = Palette("PhaseVisualOvulatoryRay");
            canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth5");
            canvas.StrokeLineCap = LineCap.Round;

            for (var index = 0; index < 12; index++)
            {
                var angle = index / 12f * FullTurn + seamlessRotation;
                var start = PointOnCircle(center, rayStart, angle);
                var end = PointOnCircle(center, rayEnd, angle);
                canvas.DrawLine(start.X, start.Y, end.X, end.Y);
            }

            canvas.FillColor = Palette("PhaseVisualOvulatoryFill");
            canvas.FillCircle(center.X, center.Y, 23f + pulse * 1.8f);

            canvas.StrokeColor = Palette("PhaseVisualOvulatoryStroke");
            canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth3");
            canvas.DrawCircle(center.X, center.Y, 21f + pulse * 1.3f);

            canvas.FillColor = Palette("PhaseVisualOvulatoryHighlight");
            canvas.FillCircle(43f, 42f, 5f + pulse);
        }

        /// <summary>The follicular artwork: a plant that grows and settles with the loop.</summary>
        private static void DrawPlant(ICanvas canvas, float progress)
        {
            var grow = 0.82f + MathF.Sin(progress * FullTurn) * 0.08f;
            var sway = MathF.Sin(progress * FullTurn) * 2.4f;
            var stemTop = 76f - 43f * grow;

            canvas.FillColor = Palette("PhaseVisualFollicularShadow");
            canvas.FillEllipse(28f, 78f, 44f, 10f);

            canvas.StrokeColor = Palette("PhaseVisualFollicularStroke");
            canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth5");
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(50f, 80f, 50f + sway, stemTop);

            DrawLeaf(
                canvas,
                new PointF(50f, 62f),
                new PointF(22f - sway, 47f),
                17f,
                Palette("PhaseVisualFollicularLeafOne"),
                Palette("PhaseVisualFollicularStroke"));

            DrawLeaf(
                canvas,
                new PointF(50f, 54f),
                new PointF(77f + sway, 37f),
                16f,
                Palette("PhaseVisualFollicularLeafTwo"),
                Palette("PhaseVisualFollicularStroke"));

            DrawLeaf(
                canvas,
                new PointF(50f + sway, stemTop + 3f),
                new PointF(50f + sway, 18f),
                13f,
                Palette("PhaseVisualFollicularLeafThree"),
                Palette("PhaseVisualFollicularStroke"));

            canvas.FillColor = Palette("PhaseVisualAccent");
            canvas.FillCircle(50f + sway, stemTop + 4f, 3f);
        }

        /// <summary>The luteal artwork: a moon whose star field brightens as the loop runs.</summary>
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

            canvas.FillColor = Palette("PhaseVisualLutealFill");
            canvas.FillPath(moon);

            canvas.StrokeColor = Palette("PhaseVisualLutealStroke");
            canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth3");
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(moon);

            canvas.StrokeColor = Palette("PhaseVisualLutealDetail");
            canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth1_6");
            canvas.DrawArc(45f, 21f, 28f, 56f, 101f, 257f, false, false);

            canvas.RestoreState();

            canvas.FillColor = Palette("PhaseVisualAccent");
            canvas.FillCircle(68f, 25f + bob * 0.5f, 2f + glow * 0.7f);
            canvas.FillCircle(78f, 40f - bob * 0.4f, 1.7f + glow * 0.6f);
            canvas.FillCircle(68f, 65f + bob * 0.35f, 1.8f + glow * 0.5f);

            DrawStar(canvas, 28f, 25f, 4.8f + glow);
            DrawStar(canvas, 78f, 72f, 4f + glow * 0.8f);
        }

        /// <summary>One drop, positioned by its top point so the callers place shapes rather than compute their centres.</summary>
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
            canvas.StrokeSize = MathF.Max(
                (float)AppThemeResources.GetDouble("UiStrokeWidth1_7"),
                size * (float)AppThemeResources.GetDouble("PhaseVisualWaterlineStrokeScale"));
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(drop);

            canvas.FillColor = Palette("PhaseVisualHighlight");
            canvas.FillEllipse(centerX - halfWidth * 0.36f, top.Y + size * 0.35f, halfWidth * 0.26f, size * 0.26f);
        }

        /// <summary>One star in the luteal night sky.</summary>
        private static void DrawStar(ICanvas canvas, float centerX, float centerY, float size)
        {
            canvas.StrokeColor = Palette("PhaseVisualAccent");
            canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth2");
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(centerX - size, centerY, centerX + size, centerY);
            canvas.DrawLine(centerX, centerY - size, centerX, centerY + size);
        }

        /// <summary>One leaf on the follicular plant, drawn from its root towards its tip.</summary>
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
            path.CurveTo(tip.X + normalX * 0.12f, tip.Y + normalY * 0.12f, rightControl.X, rightControl.Y, root.X,
                root.Y);
            path.Close();

            canvas.FillColor = fill;
            canvas.FillPath(path);
            canvas.StrokeColor = stroke;
            canvas.StrokeSize = (float)AppThemeResources.GetDouble("UiStrokeWidth2");
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
        }

        /// <summary>The point on a circle, so the artwork can place shapes by angle instead of by hand.</summary>
        private static PointF PointOnCircle(PointF center, float radius, float angleRadians)
        {
            return new PointF(
                center.X + radius * MathF.Cos(angleRadians),
                center.Y + radius * MathF.Sin(angleRadians));
        }

        /// <summary>A colour from the theme's palette by resource key, so the artwork follows the app's colours instead of</summary>
        /// hard-coding them.</summary>
        private static Color Palette(string key)
        {
            return AppThemeResources.GetColor(key);
        }
    }
}
