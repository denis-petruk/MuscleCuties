using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Shared;

internal static class ModalTransition
{
    internal static void PlayShow(ContentView modal, double? slideDistance = null)
    {
        modal.CancelAnimations();
        modal.Opacity = AppThemeResources.GetDouble("UiOpacityHidden");

        var card = FindCard(modal);
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

    internal static void PlayHide(ContentView modal, Action? onComplete = null)
    {
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

    private static Border? FindCard(ContentView modal)
    {
        if (modal.Content is Layout container)
            return container.Children.OfType<Border>().FirstOrDefault();
        return modal.Content as Border;
    }
}
