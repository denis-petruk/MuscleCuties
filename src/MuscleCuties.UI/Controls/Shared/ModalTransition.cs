
namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     The one place sheet show and hide animations are defined.
/// </summary>
internal static class ModalTransition
{
    /// <summary>Slides and fades a sheet in, with the distance defaulting to the sheet's own height.</summary>
    internal static void PlayShow(ContentView modal, double? slideDistance = null)
    {
        modal.CancelAnimations();
        var card = FindCard(modal);

        if (MotionSettings.ReduceMotion)
        {
            // Reduce Motion keeps the same end state without translation, scale or fade.
            modal.Opacity = AppThemeResources.GetDouble("UiOpacityFull");
            if (card != null)
            {
                card.CancelAnimations();
                card.TranslationY = AppThemeResources.GetDouble("UiMotionOffsetZero");
                card.Scale = AppThemeResources.GetDouble("UiScaleFull");
            }

            return;
        }

        modal.Opacity = AppThemeResources.GetDouble("UiOpacityHidden");
        if (card != null)
        {
            card.CancelAnimations();
            card.TranslationY = slideDistance ?? AppThemeResources.GetDouble("UiMotionOffsetModalShow");
            card.Scale = AppThemeResources.GetDouble("UiScaleModalStart");
        }

        modal.Dispatcher.Dispatch(async () =>
        {
            try
            {
                var tasks = new List<Task>
                {
                    modal.FadeToAsync(AppThemeResources.GetDouble("UiOpacityFull"),
                        (uint)AppThemeResources.GetDouble("UiMotionDuration250"), Easing.CubicOut)
                };
                if (card != null)
                {
                    var duration = (uint)AppThemeResources.GetDouble("UiMotionDuration300");
                    tasks.Add(card.TranslateToAsync(AppThemeResources.GetDouble("UiMotionOffsetZero"),
                        AppThemeResources.GetDouble("UiMotionOffsetZero"), duration, Easing.CubicOut));
                    tasks.Add(card.ScaleToAsync(AppThemeResources.GetDouble("UiScaleFull"), duration, Easing.CubicOut));
                }

                await Task.WhenAll(tasks);
            }
            catch
            {
                modal.Opacity = AppThemeResources.GetDouble("UiOpacityFull");
                if (card != null)
                {
                    card.TranslationY = AppThemeResources.GetDouble("UiMotionOffsetZero");
                    card.Scale = AppThemeResources.GetDouble("UiScaleFull");
                }
            }
        });
    }

    /// <summary>Slides and fades a sheet out and only then runs the caller's completion, which is the one safe moment to hide</summary>
    /// or dispose what was animating.</summary>
    internal static void PlayHide(ContentView modal, Action? onComplete = null)
    {
        modal.CancelAnimations();
        FindCard(modal)?.CancelAnimations();

        if (MotionSettings.ReduceMotion)
        {
            modal.Opacity = AppThemeResources.GetDouble("UiOpacityHidden");
            onComplete?.Invoke();
            return;
        }

        modal.Dispatcher.Dispatch(async () =>
        {
            try
            {
                var card = FindCard(modal);
                var duration = (uint)AppThemeResources.GetDouble("UiMotionDuration200");
                var tasks = new List<Task>
                {
                    modal.FadeToAsync(AppThemeResources.GetDouble("UiOpacityHidden"), duration, Easing.CubicIn)
                };
                if (card != null)
                {
                    tasks.Add(card.TranslateToAsync(AppThemeResources.GetDouble("UiMotionOffsetZero"),
                        AppThemeResources.GetDouble("UiMotionOffsetModalHide"), duration, Easing.CubicIn));
                    tasks.Add(card.ScaleToAsync(AppThemeResources.GetDouble("UiScaleModalStart"), duration, Easing.CubicIn));
                }

                await Task.WhenAll(tasks);
            }
            catch
            {
            }
            finally
            {
                onComplete?.Invoke();
            }
        });
    }

    /// <summary>The card surface that actually slides; a sheet without one transitions as a whole.</summary>
    internal static Border? FindCard(ContentView modal)
    {
        if (modal.Content is Layout container)
            return container.Children.OfType<Border>().FirstOrDefault();
        return modal.Content as Border;
    }
}
