namespace MuscleCuties.App.Controls.Shared;

public partial class PhaseSaluteOverlay : ContentView
{
    public static readonly BindableProperty IconSourceProperty = BindableProperty.Create(
        nameof(IconSource),
        typeof(string),
        typeof(PhaseSaluteOverlay),
        "phase_follicular_plant.json");

    public PhaseSaluteOverlay()
    {
        InitializeComponent();
    }

    public string IconSource
    {
        get => (string)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public async Task PlayAsync(string? iconSource = null)
    {
        if (!string.IsNullOrWhiteSpace(iconSource))
            IconSource = iconSource;

        IsVisible = true;
        OverlayLayer.Opacity = 0;

        LeftBurst.Opacity = 0;
        RightBurst.Opacity = 0;
        LeftBurst.TranslationX = -150;
        RightBurst.TranslationX = 150;
        LeftBurst.Scale = 0.64;
        RightBurst.Scale = 0.64;
        LeftBurst.Rotation = -24;
        RightBurst.Rotation = 24;

        ResetSpark(LeftSparkTop, -90, -32, 0.6);
        ResetSpark(LeftSparkBottom, -76, 34, 0.5);
        ResetSpark(RightSparkTop, 88, -38, 0.58);
        ResetSpark(RightSparkBottom, 74, 30, 0.62);

        await Task.WhenAll(
            OverlayLayer.FadeToAsync(1, 70, Easing.CubicOut),
            LeftBurst.FadeToAsync(0.98, 120, Easing.CubicOut),
            RightBurst.FadeToAsync(0.98, 120, Easing.CubicOut),
            LeftBurst.TranslateToAsync(24, 0, 360, Easing.SpringOut),
            RightBurst.TranslateToAsync(-24, 0, 360, Easing.SpringOut),
            LeftBurst.ScaleToAsync(1.12, 280, Easing.CubicOut),
            RightBurst.ScaleToAsync(1.12, 280, Easing.CubicOut),
            AnimateSparkAsync(LeftSparkTop, 36, -78),
            AnimateSparkAsync(LeftSparkBottom, 42, 76),
            AnimateSparkAsync(RightSparkTop, -38, -84),
            AnimateSparkAsync(RightSparkBottom, -44, 72));

        await Task.WhenAll(
            LeftBurst.RotateToAsync(10, 170, Easing.CubicInOut),
            RightBurst.RotateToAsync(-10, 170, Easing.CubicInOut),
            LeftBurst.ScaleToAsync(0.98, 170, Easing.CubicInOut),
            RightBurst.ScaleToAsync(0.98, 170, Easing.CubicInOut));

        await Task.Delay(260);

        await Task.WhenAll(
            OverlayLayer.FadeToAsync(0, 240, Easing.CubicIn),
            LeftBurst.TranslateToAsync(-110, 0, 240, Easing.CubicIn),
            RightBurst.TranslateToAsync(110, 0, 240, Easing.CubicIn));

        IsVisible = false;
    }

    private static void ResetSpark(VisualElement spark, double translationX, double translationY, double scale)
    {
        spark.Opacity = 0;
        spark.TranslationX = translationX;
        spark.TranslationY = translationY;
        spark.Scale = scale;
    }

    private static async Task AnimateSparkAsync(VisualElement spark, double translationX, double translationY)
    {
        await Task.WhenAll(
            spark.FadeToAsync(0.9, 90, Easing.CubicOut),
            spark.TranslateToAsync(translationX, translationY, 330, Easing.CubicOut),
            spark.ScaleToAsync(1.35, 220, Easing.CubicOut));

        await spark.FadeToAsync(0, 160, Easing.CubicIn);
    }
}
