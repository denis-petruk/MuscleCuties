namespace MuscleCuties.App.Controls.Shared;

internal static class ModalTransition
{
    internal static void PlayShow(ContentView modal, double slideDistance = 40)
    {
        modal.CancelAnimations();
        modal.Opacity = 0;

        var card = FindCard(modal);
        if (card != null)
        {
            card.CancelAnimations();
            card.TranslationY = slideDistance;
            card.Scale = 0.97;
        }

        modal.Dispatcher.Dispatch(async () =>
        {
            try
            {
                var tasks = new List<Task> { modal.FadeToAsync(1, 250, Easing.CubicOut) };
                if (card != null)
                {
                    tasks.Add(card.TranslateToAsync(0, 0, 300, Easing.CubicOut));
                    tasks.Add(card.ScaleToAsync(1, 300, Easing.CubicOut));
                }

                await Task.WhenAll(tasks);
            }
            catch
            {
                modal.Opacity = 1;
                if (card != null)
                {
                    card.TranslationY = 0;
                    card.Scale = 1;
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
                var tasks = new List<Task> { modal.FadeToAsync(0, 200, Easing.CubicIn) };
                if (card != null)
                {
                    tasks.Add(card.TranslateToAsync(0, 20, 200, Easing.CubicIn));
                    tasks.Add(card.ScaleToAsync(0.97, 200, Easing.CubicIn));
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
