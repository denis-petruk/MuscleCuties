using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Views;
using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.App.Pages;

internal static class PageLoadExtensions
{
    private const double InteractionBudgetMilliseconds = 100;
    private static readonly ConditionalWeakTable<Page, FirstLayoutTracker> FirstLayoutTrackers = new();

    public static TPage CreateWithTiming<TPage, TViewModel>(IServiceProvider services, Func<TViewModel, TPage> create)
        where TPage : Page
        where TViewModel : class
    {
        var started = Stopwatch.GetTimestamp();
        var viewModel = services.GetRequiredService<TViewModel>();
        WriteTiming(typeof(TPage).Name, "ViewModel DI", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return create(viewModel);
    }

    public static void InitializeWithTiming(this Page page, Action initialize)
    {
        var stopwatch = Stopwatch.StartNew();
        initialize();
        WriteTiming(page.GetType().Name, "XAML initialization", stopwatch.Elapsed.TotalMilliseconds);
    }

    public static void BindWithTiming(this Page page, object viewModel, long constructorStarted)
    {
        var started = Stopwatch.GetTimestamp();
        page.BindingContext = viewModel;
        WriteTiming(page.GetType().Name, "binding attachment", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        WriteTiming(page.GetType().Name, "constructor", Stopwatch.GetElapsedTime(constructorStarted).TotalMilliseconds);
    }

    public static async ValueTask LoadIfNeededAsync(this LazyView view, bool requested)
    {
        if (!requested || view.HasLazyViewLoaded)
            return;

        var started = Stopwatch.GetTimestamp();
        await view.LoadViewAsync();
        WriteTiming(
            view.Content.GetType().Name,
            "deferred view initialization",
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    public static void BeginPageLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        _ = GetFirstLayoutTask(page, pageName);
        Trace.WriteLine($"[Performance][{pageName}] Page entered; data load started independently of view inflation.");
        _ = RunSafelyAsync(page, pageName, "data load", loadAsync);
    }

    public static void BeginDeferredLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        var firstLayout = GetFirstLayoutTask(page, pageName);
        Trace.WriteLine($"[Performance][{pageName}] Deferred view load is waiting for first layout.");
        _ = RunDeferredLoadSafelyAsync(page, pageName, firstLayout, loadAsync);
    }

    private static async Task RunDeferredLoadSafelyAsync(
        Page page,
        string pageName,
        Task firstLayout,
        Func<Task> loadAsync)
    {
        await firstLayout.ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(
            () => RunSafelyAsync(page, pageName, "deferred view load", loadAsync));
    }

    private static async Task RunSafelyAsync(
        Page page,
        string pageName,
        string operation,
        Func<Task> loadAsync)
    {
        var stopwatch = Stopwatch.StartNew();
        Trace.WriteLine(
            $"[Performance][{pageName}] {operation} started; size={page.Width:0}x{page.Height:0}.");

        if (page.BindingContext is IPageLoadAware loadAware)
            loadAware.IsLoadError = false;

        try
        {
            await loadAsync();
            WriteTiming(pageName, operation, stopwatch.Elapsed.TotalMilliseconds);
            Trace.WriteLine($"[Performance][{pageName}] {operation} finished at size={page.Width:0}x{page.Height:0}.");
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[Performance][{pageName}] {operation} failed after {stopwatch.ElapsedMilliseconds} ms: {exception}");

            if (page.BindingContext is IPageLoadAware aware)
                aware.IsLoadError = true;
        }
    }

    private static Task GetFirstLayoutTask(Page page, string pageName)
    {
        if (page.Width > 0 && page.Height > 0)
        {
            Trace.WriteLine($"[Performance][{pageName}] Page was already arranged at {page.Width:0}x{page.Height:0}.");
            return Task.CompletedTask;
        }

        var tracker = FirstLayoutTrackers.GetValue(page, static _ => new FirstLayoutTracker());
        if (tracker.IsSubscribed)
            return tracker.Completion.Task;

        tracker.IsSubscribed = true;
        tracker.Started = Stopwatch.GetTimestamp();
        page.SizeChanged += OnFirstLayout;
        CompleteIfArranged();
        return tracker.Completion.Task;

        void OnFirstLayout(object? sender, EventArgs args)
        {
            CompleteIfArranged();
        }

        void CompleteIfArranged()
        {
            if (page.Width <= 0 || page.Height <= 0 || tracker.Completion.Task.IsCompleted)
                return;

            page.SizeChanged -= OnFirstLayout;
            WriteTiming(
                pageName,
                "first layout after page entry",
                Stopwatch.GetElapsedTime(tracker.Started).TotalMilliseconds);
            Trace.WriteLine($"[Performance][{pageName}] First layout size={page.Width:0}x{page.Height:0}.");
            tracker.Completion.TrySetResult(true);
        }
    }

    private static void WriteTiming(string component, string operation, double elapsedMilliseconds)
    {
        var budgetState = elapsedMilliseconds <= InteractionBudgetMilliseconds
            ? "within budget"
            : $"over {InteractionBudgetMilliseconds:0} ms budget";

        Trace.WriteLine(
            $"[Performance][{component}] {operation} completed in {elapsedMilliseconds:F1} ms ({budgetState}).");
    }

    private sealed class FirstLayoutTracker
    {
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsSubscribed { get; set; }
        public long Started { get; set; }
    }
}
