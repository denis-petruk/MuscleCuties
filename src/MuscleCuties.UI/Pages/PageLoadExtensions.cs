using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using MuscleCuties.UI.Controls.Injury;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages;

/// <summary>Page plumbing shared by every screen: timed construction and binding, deferred and lazy loads that</summary>
/// report failure on the page, per-page load state, and theme-change observers.</summary>
internal static class PageLoadExtensions
{
    private const double InteractionBudgetMilliseconds = 100;
    private const int PostLayoutRenderDelayMilliseconds = 50;
    private static readonly bool ConsoleProfilingEnabled =
        Environment.GetEnvironmentVariable("MUSCLECUTIES_PERF_CONSOLE") == "1";
    private static readonly ConditionalWeakTable<Page, FirstLayoutTracker> FirstLayoutTrackers = new();
    private static readonly ConditionalWeakTable<LazyView, LazyViewLoadState> LazyViewLoads = new();
    private static readonly ConditionalWeakTable<Page, PageLoadErrorState> PageLoadErrors = new();

    /// <summary>Resolves the view model and creates the page, writing the timings when performance profiling is on.</summary>
    public static TPage CreateWithTiming<TPage, TViewModel>(IServiceProvider services, Func<TViewModel, TPage> create)
        where TPage : Page
        where TViewModel : class
    {
        var started = Stopwatch.GetTimestamp();
        var viewModel = services.GetRequiredService<TViewModel>();
        WriteTiming(typeof(TPage).Name, "ViewModel DI", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return create(viewModel);
    }

    /// <summary>Runs the XAML initialisation and writes how long it took.</summary>
    public static void InitializeWithTiming(this Page page, Action initialize)
    {
        var stopwatch = Stopwatch.StartNew();
        initialize();
        WriteTiming(page.GetType().Name, "XAML initialization", stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>Binds the page and writes the whole construction time, from the timestamp the caller captured.</summary>
    public static void BindWithTiming(this Page page, object viewModel, long constructorStarted)
    {
        var started = Stopwatch.GetTimestamp();
        page.BindingContext = viewModel;
        WriteTiming(page.GetType().Name, "binding attachment", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        WriteTiming(page.GetType().Name, "constructor", Stopwatch.GetElapsedTime(constructorStarted).TotalMilliseconds);
    }

    /// <summary>Loads a lazy view only when it is needed, and lets concurrent callers await the same pass instead of</summary>
    /// loading it twice.</summary>
    public static async ValueTask LoadIfNeededAsync(this LazyView view, bool requested)
    {
        if (!requested || view.HasLazyViewLoaded)
            return;

        var state = LazyViewLoads.GetValue(view, static _ => new LazyViewLoadState());
        TaskCompletionSource? start = null;
        Task loadTask;
        lock (state)
        {
            if (view.HasLazyViewLoaded)
                return;

            if (state.Task is null)
            {
                start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                state.Task = start.Task;
            }

            loadTask = state.Task;
        }

        if (start is not null)
            _ = LoadLazyViewAsync(view, state, start);

        await loadTask;
    }

    /// <summary>The one load of a lazy view, writing its timing and completing the shared task even when the view</summary>
    /// fails to build.</summary>
    private static async Task LoadLazyViewAsync(LazyView view, LazyViewLoadState state, TaskCompletionSource completion)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await view.LoadViewAsync();
            WriteTiming(
                view.Content.GetType().Name,
                "deferred view initialization",
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            lock (state)
                if (ReferenceEquals(state.Task, completion.Task))
                    state.Task = null;
            completion.TrySetException(exception);
        }
    }

    /// <summary>
    /// Queues a data load after first layout. <paramref name="isCurrent"/> is evaluated on the UI
    /// thread immediately before the load wrapper runs; a stale appearance is skipped entirely so it
    /// cannot reset loading/error state or start work on a page that has already left.
    /// </summary>
    public static void BeginPageLoad(this Page page, Func<Task> loadAsync, Func<bool>? isCurrent = null)
    {
        var pageName = page.GetType().Name;
        var firstLayout = GetFirstLayoutTask(page, pageName);
        Trace.WriteLine($"[Performance][{pageName}] Page entered; data load queued after first layout.");
        _ = RunAfterFirstLayoutSafelyAsync(page, pageName, firstLayout, loadAsync, isCurrent);
    }

    /// <summary>Runs the page's load after its first layout, logging a failure.</summary>
    private static async Task RunAfterFirstLayoutSafelyAsync(
        Page page,
        string pageName,
        Task firstLayout,
        Func<Task> loadAsync,
        Func<bool>? isCurrent)
    {
        await firstLayout.ConfigureAwait(false);
        // Defer rendering by 50ms so native Shell slide animation completes without UI contention.
        await Task.Delay(PostLayoutRenderDelayMilliseconds).ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (isCurrent is not null && !isCurrent())
            {
                Trace.WriteLine(
                    $"[Performance][{pageName}] Deferred data load skipped; the page appearance changed.");
                return Task.CompletedTask;
            }

            return RunSafelyAsync(page, pageName, "data load", loadAsync);
        });
    }

    /// <summary>Runs a page's below-the-fold load after the first layout, off the navigation path, so the first frame</summary>
    /// is not delayed by it.</summary>
    public static void BeginDeferredLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        var firstLayout = GetFirstLayoutTask(page, pageName);
        Trace.WriteLine($"[Performance][{pageName}] Deferred view load is waiting for first layout.");
        _ = RunDeferredLoadSafelyAsync(page, pageName, firstLayout, loadAsync);
    }

    /// <summary>Runs a deferred load once the page has laid out, logging a failure.</summary>
    private static async Task RunDeferredLoadSafelyAsync(
        Page page,
        string pageName,
        Task firstLayout,
        Func<Task> loadAsync)
    {
        await firstLayout.ConfigureAwait(false);
        // SizeChanged can run before the compositor presents the first frame.
        // Defer rendering by 50ms before inflating deferred XAML on the UI thread.
        await Task.Delay(PostLayoutRenderDelayMilliseconds).ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => RunSafelyAsync(page, pageName, "deferred view load", loadAsync));
    }

    /// <summary>Runs a page operation, logging and swallowing its failure.</summary>
    private static async Task RunSafelyAsync(
        Page page,
        string pageName,
        string operation,
        Func<Task> loadAsync)
    {
        var stopwatch = Stopwatch.StartNew();
        Trace.WriteLine(
            $"[Performance][{pageName}] {operation} started; size={page.Width:0}x{page.Height:0}.");

        var errorState = PageLoadErrors.GetValue(page, static _ => new PageLoadErrorState());
        var isDataLoad = operation == "data load";
        if (page.BindingContext is IPageLoadAware loadAware)
        {
            if (isDataLoad)
            {
                errorState.DataFailed = false;
                loadAware.IsLoadError = errorState.DeferredFailed;
            }
            else
            {
                errorState.DeferredFailed = false;
                if (errorState.DataFailed)
                    loadAware.IsLoadError = true;
            }
        }

        try
        {
            await loadAsync();
            if (isDataLoad && page.BindingContext is IPageLoadAware completedLoad)
                errorState.DataFailed = completedLoad.IsLoadError && !errorState.DeferredFailed;
            WriteTiming(pageName, operation, stopwatch.Elapsed.TotalMilliseconds);
            Trace.WriteLine($"[Performance][{pageName}] {operation} finished at size={page.Width:0}x{page.Height:0}.");
        }
        catch (Exception exception)
        {
            Trace.WriteLine(
                $"[Performance][{pageName}] {operation} failed after {stopwatch.ElapsedMilliseconds} ms ({exception.GetType().Name}).");

            if (isDataLoad)
                errorState.DataFailed = true;
            else
                errorState.DeferredFailed = true;

            if (page.BindingContext is IPageLoadAware aware)
                aware.IsLoadError = true;
        }
    }

    /// <summary>A task that completes when the page has been laid out, or immediately when it already has been.</summary>
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

    /// <summary>Writes one timing, marking it as over or within the interaction budget.</summary>
    private static void WriteTiming(string component, string operation, double elapsedMilliseconds)
    {
        var budgetState = elapsedMilliseconds <= InteractionBudgetMilliseconds
            ? "within budget"
            : $"over {InteractionBudgetMilliseconds:0} ms budget";

        var message =
            $"[Performance][{component}] {operation} completed in {elapsedMilliseconds:F1} ms ({budgetState}).";
        Trace.WriteLine(message);
        if (ConsoleProfilingEnabled)
            Console.WriteLine(message);
    }

    /// <summary>Shows or hides the injury modal and tracks its load.</summary>
    public static async Task UpdateInjuryModalAsync(
        LazyView injuryModalLazy,
        Func<Task?> getLoadTask,
        Action<Task?> setLoadTask,
        bool isVisible,
        InjuryLogViewModel injuryLogVm,
        ICommand closeCommand)
    {
        if (!isVisible && !injuryModalLazy.HasLazyViewLoaded)
            return;

        if (isVisible)
            try
            {
                var task = getLoadTask() ?? injuryModalLazy.LoadIfNeededAsync(true).AsTask();
                setLoadTask(task);
                await task;
            }
            catch
            {
                setLoadTask(null);
                throw;
            }

        if (!injuryModalLazy.HasLazyViewLoaded)
            return;

        var modal = (InjuryLogModal)injuryModalLazy.Content;
        if (modal.BindingContext != injuryLogVm)
            modal.BindingContext = injuryLogVm;
        modal.CloseCommand = closeCommand;
        modal.IsOpen = isVisible;
    }

    /// <summary>Back closes an open modal and consumes the press; returns false so the page handles it when no modal is</summary>
    /// open.</summary>
    public static bool TryCloseModalOnBack(bool isModalVisible, ICommand closeCommand)
    {
        if (!isModalVisible)
            return false;

        closeCommand.Execute(null);
        return true;
    }

    /// <summary>Initializes the view model, binds it, and returns it.</summary>
    public static TViewModel InitializeAndBind<TViewModel>(
        this Page page,
        Action initialize,
        TViewModel viewModel,
        Action? beforeBind = null)
        where TViewModel : class
    {
        var started = Stopwatch.GetTimestamp();
        page.InitializeWithTiming(initialize);
        beforeBind?.Invoke();
        page.BindWithTiming(viewModel, started);
        return viewModel;
    }

    /// <summary>Re-subscribes the handler to a view model whose binding was replaced.</summary>
    public static void ReattachPropertyChanged(
        this INotifyPropertyChanged viewModel,
        PropertyChangedEventHandler handler)
    {
        viewModel.PropertyChanged -= handler;
        viewModel.PropertyChanged += handler;
    }

    /// <summary>Whether the app is currently on its dark appearance.</summary>
    public static bool IsDarkTheme()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }

    /// <summary>Observes theme changes for the page's lifetime and forwards the new darkness to its colour</summary>
    /// refresh.</summary>
    public static ThemeChangeObserver CreateThemeObserver(this Page page, Action<bool> refreshThemeColors)
    {
        return new ThemeChangeObserver(theme => refreshThemeColors(theme == AppTheme.Dark));
    }

    /// <summary>Presents a modal, recording a failure on the view model instead of throwing.</summary>
    public static async Task PresentModalSafelyAsync(IPageLoadAware viewModel, string componentName,
        Func<Task> presentAsync)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(presentAsync);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[{componentName}] Could not present view ({exception.GetType().Name}).");
            viewModel.IsLoadError = true;
        }
    }

    /// <summary>Times a page's first layout once, so the deferred work can wait for something on screen.</summary>
    private sealed class FirstLayoutTracker
    {
        /// <summary>Completes once the modal interaction finishes.</summary>
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Whether the layout event is already wired, so it is attached once.</summary>
        public bool IsSubscribed { get; set; }
        /// <summary>Timestamp the first layout wait began, for the timing line.</summary>
        public long Started { get; set; }
    }

    /// <summary>The load a lazy view is currently running, so concurrent requests share one pass.</summary>
    private sealed class LazyViewLoadState
    {
        /// <summary>The load in flight, or null when the view has none.</summary>
        public Task? Task { get; set; }
    }

    /// <summary>Which of a page's two load phases failed, so the retry affordance can name the right one.</summary>
    private sealed class PageLoadErrorState
    {
        /// <summary>Whether the primary data load failed.</summary>
        public bool DataFailed { get; set; }
        /// <summary>Whether a deferred card failed to load.</summary>
        public bool DeferredFailed { get; set; }
    }
}
