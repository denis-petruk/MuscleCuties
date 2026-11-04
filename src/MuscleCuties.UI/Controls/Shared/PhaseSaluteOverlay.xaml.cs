using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Shared;

public partial class PhaseSaluteOverlay : ContentView
{
    public static readonly BindableProperty IconSourceProperty = BindableProperty.Create(
        nameof(IconSource),
        typeof(string),
        typeof(PhaseSaluteOverlay),
        "Animations/Cycle/phase_follicular_plant.json");

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
        OverlayLayer.Opacity = AppThemeResources.GetDouble("UiOpacityHidden");

        LeftBurst.Opacity = AppThemeResources.GetDouble("UiOpacityHidden");
        RightBurst.Opacity = AppThemeResources.GetDouble("UiOpacityHidden");
        LeftBurst.TranslationX = -AppThemeResources.GetDouble("UiMotionOffsetCelebrationEntry");
        RightBurst.TranslationX = AppThemeResources.GetDouble("UiMotionOffsetCelebrationEntry");
        LeftBurst.Scale = AppThemeResources.GetDouble("UiScaleCelebrationStart");
        RightBurst.Scale = AppThemeResources.GetDouble("UiScaleCelebrationStart");
        LeftBurst.Rotation = -AppThemeResources.GetDouble("UiMotionRotationCelebrationStart");
        RightBurst.Rotation = AppThemeResources.GetDouble("UiMotionRotationCelebrationStart");

        ResetSpark(LeftSparkTop,
            AppThemeResources.GetDouble("UiMotionSparkLeftTopStartX"),
            AppThemeResources.GetDouble("UiMotionSparkLeftTopStartY"),
            AppThemeResources.GetDouble("UiScaleSparkMedium"));
        ResetSpark(LeftSparkBottom,
            AppThemeResources.GetDouble("UiMotionSparkLeftBottomStartX"),
            AppThemeResources.GetDouble("UiMotionSparkLeftBottomStartY"),
            AppThemeResources.GetDouble("UiScaleSparkSmall"));
        ResetSpark(RightSparkTop,
            AppThemeResources.GetDouble("UiMotionSparkRightTopStartX"),
            AppThemeResources.GetDouble("UiMotionSparkRightTopStartY"),
            AppThemeResources.GetDouble("UiScaleSparkTall"));
        ResetSpark(RightSparkBottom,
            AppThemeResources.GetDouble("UiMotionSparkRightBottomStartX"),
            AppThemeResources.GetDouble("UiMotionSparkRightBottomStartY"),
            AppThemeResources.GetDouble("UiScaleSparkLarge"));

        await Task.WhenAll(
            OverlayLayer.FadeToAsync(AppThemeResources.GetDouble("UiOpacityFull"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration70"), Easing.CubicOut),
            LeftBurst.FadeToAsync(AppThemeResources.GetDouble("UiOpacityCelebration"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration120"), Easing.CubicOut),
            RightBurst.FadeToAsync(AppThemeResources.GetDouble("UiOpacityCelebration"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration120"), Easing.CubicOut),
            LeftBurst.TranslateToAsync(AppThemeResources.GetDouble("UiMotionOffsetCelebrationSettle"),
                AppThemeResources.GetDouble("UiMotionOffsetZero"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration360"), Easing.SpringOut),
            RightBurst.TranslateToAsync(-AppThemeResources.GetDouble("UiMotionOffsetCelebrationSettle"),
                AppThemeResources.GetDouble("UiMotionOffsetZero"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration360"), Easing.SpringOut),
            LeftBurst.ScaleToAsync(AppThemeResources.GetDouble("UiScaleCelebrationPeak"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration280"), Easing.CubicOut),
            RightBurst.ScaleToAsync(AppThemeResources.GetDouble("UiScaleCelebrationPeak"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration280"), Easing.CubicOut),
            AnimateSparkAsync(LeftSparkTop,
                AppThemeResources.GetDouble("UiMotionSparkLeftTopEndX"),
                AppThemeResources.GetDouble("UiMotionSparkLeftTopEndY")),
            AnimateSparkAsync(LeftSparkBottom,
                AppThemeResources.GetDouble("UiMotionSparkLeftBottomEndX"),
                AppThemeResources.GetDouble("UiMotionSparkLeftBottomEndY")),
            AnimateSparkAsync(RightSparkTop,
                AppThemeResources.GetDouble("UiMotionSparkRightTopEndX"),
                AppThemeResources.GetDouble("UiMotionSparkRightTopEndY")),
            AnimateSparkAsync(RightSparkBottom,
                AppThemeResources.GetDouble("UiMotionSparkRightBottomEndX"),
                AppThemeResources.GetDouble("UiMotionSparkRightBottomEndY")));

        await Task.WhenAll(
            LeftBurst.RotateToAsync(AppThemeResources.GetDouble("UiMotionRotationCelebrationSettle"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration170"), Easing.CubicInOut),
            RightBurst.RotateToAsync(-AppThemeResources.GetDouble("UiMotionRotationCelebrationSettle"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration170"), Easing.CubicInOut),
            LeftBurst.ScaleToAsync(AppThemeResources.GetDouble("UiScaleCelebrationSettle"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration170"), Easing.CubicInOut),
            RightBurst.ScaleToAsync(AppThemeResources.GetDouble("UiScaleCelebrationSettle"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration170"), Easing.CubicInOut));

        await Task.Delay((int)AppThemeResources.GetDouble("UiMotionDuration260"));

        await Task.WhenAll(
            OverlayLayer.FadeToAsync(AppThemeResources.GetDouble("UiOpacityHidden"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration240"), Easing.CubicIn),
            LeftBurst.TranslateToAsync(-AppThemeResources.GetDouble("UiMotionOffsetCelebrationExit"),
                AppThemeResources.GetDouble("UiMotionOffsetZero"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration240"), Easing.CubicIn),
            RightBurst.TranslateToAsync(AppThemeResources.GetDouble("UiMotionOffsetCelebrationExit"),
                AppThemeResources.GetDouble("UiMotionOffsetZero"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration240"), Easing.CubicIn));

        IsVisible = false;
    }

    private static void ResetSpark(VisualElement spark, double translationX, double translationY, double scale)
    {
        spark.Opacity = AppThemeResources.GetDouble("UiOpacityHidden");
        spark.TranslationX = translationX;
        spark.TranslationY = translationY;
        spark.Scale = scale;
    }

    private static async Task AnimateSparkAsync(VisualElement spark, double translationX, double translationY)
    {
        await Task.WhenAll(
            spark.FadeToAsync(AppThemeResources.GetDouble("UiOpacitySparkPeak"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration90"), Easing.CubicOut),
            spark.TranslateToAsync(translationX, translationY,
                (uint)AppThemeResources.GetDouble("UiMotionDuration330"), Easing.CubicOut),
            spark.ScaleToAsync(AppThemeResources.GetDouble("UiScaleSparkPeak"),
                (uint)AppThemeResources.GetDouble("UiMotionDuration220"), Easing.CubicOut));

        await spark.FadeToAsync(AppThemeResources.GetDouble("UiOpacityHidden"),
            (uint)AppThemeResources.GetDouble("UiMotionDuration160"), Easing.CubicIn);
    }
}
