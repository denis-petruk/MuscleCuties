using SkiaSharp.Extended.UI.Controls;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Phase illustration drawn from the current cycle phase's palette and shape.
/// </summary>
public class PhaseVisualView : ContentView
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(PhaseVisualView),
        string.Empty,
        propertyChanged: OnSourceChanged);

    private readonly SKLottieView _lottieView;

    private readonly Image _staticImage;

    /// <summary>Builds the still image and the Lottie layer on top of it, and ignores touches because the control is only ever</summary>
    /// decoration.
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

        Content = new Grid
        {
            Children =
            {
                _staticImage,
                _lottieView
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

    /// <summary>Chooses what the control shows for a source: the Lottie artwork for the JSON animation, the still image for</summary>
    /// anything else, and nothing at all for an empty source.
    private void UpdateVisualSource()
    {
        var source = Source?.Trim() ?? string.Empty;
        var shouldAnimate = IsAnimatedSource(source);

        _staticImage.Source = shouldAnimate || string.IsNullOrWhiteSpace(source)
            ? null
            : ImageSource.FromFile(source);
        _staticImage.IsVisible = !shouldAnimate && !string.IsNullOrWhiteSpace(source);

        if (shouldAnimate)
        {
            _lottieView.Source = new SKFileLottieImageSource { File = source };
            _lottieView.IsVisible = true;
        }
        else
        {
            _lottieView.IsVisible = false;
        }
    }

    /// <summary>The animated artwork is recognised by its file type, so a new phase needs no code to get one.</summary>
    private static bool IsAnimatedSource(string source)
    {
        return source.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }
}
