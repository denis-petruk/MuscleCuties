using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.App.Pages;

internal static class PageLoadExtensions
{
    public static TPage CreateWithTiming<TPage, TViewModel>(IServiceProvider services, Func<TViewModel, TPage> create)
        where TPage : Page
        where TViewModel : class
    {
        var started = Stopwatch.GetTimestamp();
        var viewModel = services.GetRequiredService<TViewModel>();
        Trace.WriteLine($"[Performance][{typeof(TPage).Name}] ViewModel DI resolved in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
        return create(viewModel);
    }

    public static void InitializeWithTiming(this Page page, Action initialize)
    {
        var stopwatch = Stopwatch.StartNew();
        initialize();
        Trace.WriteLine(
            $"[Performance][{page.GetType().Name}] XAML initialized in {stopwatch.ElapsedMilliseconds} ms.");
    }

    public static void BindWithTiming(this Page page, object viewModel, long constructorStarted)
    {
        var started = Stopwatch.GetTimestamp();
        page.BindingContext = viewModel;
        Trace.WriteLine($"[Performance][{page.GetType().Name}] Bindings attached in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms; constructor total={Stopwatch.GetElapsedTime(constructorStarted).TotalMilliseconds:F1} ms.");

        page.SizeChanged += OnFirstLayout;
        void OnFirstLayout(object? sender, EventArgs args)
        {
            if (page.Width <= 0 || page.Height <= 0)
                return;

            page.SizeChanged -= OnFirstLayout;
            Trace.WriteLine($"[Performance][{page.GetType().Name}] First positive layout at {Stopwatch.GetElapsedTime(constructorStarted).TotalMilliseconds:F1} ms; size={page.Width:0}x{page.Height:0}.");
        }
    }

    public static async ValueTask LoadIfNeededAsync(this LazyView view, bool requested)
    {
        if (!requested || view.HasLazyViewLoaded)
            return;

        var started = Stopwatch.GetTimestamp();
        await view.LoadViewAsync();
        Trace.WriteLine($"[Performance][{view.Content.GetType().Name}] Deferred view initialized in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms.");
    }

    public static void BeginPageLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        Trace.WriteLine($"[Performance][{pageName}] Navigation completed; data load queued.");
        var queuedAt = Stopwatch.GetTimestamp();
        page.Dispatcher.Dispatch(() => _ = RunSafelyAsync(page, pageName, loadAsync, queuedAt));
    }

    private static async Task RunSafelyAsync(Page page, string pageName, Func<Task> loadAsync, long queuedAt)
    {
        var stopwatch = Stopwatch.StartNew();
        Trace.WriteLine(
            $"[Performance][{pageName}] Data load started; dispatcher wait={Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:F1} ms; size={page.Width:0}x{page.Height:0}.");

        if (page.BindingContext is IPageLoadAware loadAware)
            loadAware.IsLoadError = false;

        try
        {
            await loadAsync();
            Trace.WriteLine(
                $"[Performance][{pageName}] Data load completed in {stopwatch.ElapsedMilliseconds} ms; " +
                $"size={page.Width:0}x{page.Height:0}.");
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[Performance][{pageName}] Data load failed after {stopwatch.ElapsedMilliseconds} ms: {exception}");

            if (page.BindingContext is IPageLoadAware aware)
                aware.IsLoadError = true;
        }
    }
}
