using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using MuscleCuties.UI.Controls.Injury;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Common;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Pages;

internal static class PageLoadExtensions
{
    private const double InteractionBudgetMilliseconds = 100;
    private const int PostLayoutRenderDelayMilliseconds = 50;
    private static readonly bool ConsoleProfilingEnabled =
        Environment.GetEnvironmentVariable("MUSCLECUTIES_PERF_CONSOLE") == "1";
    private static readonly ConditionalWeakTable<Page, FirstLayoutTracker> FirstLayoutTrackers = new();
    private static readonly ConditionalWeakTable<LazyView, LazyViewLoadState> LazyViewLoads = new();
    private static readonly ConditionalWeakTable<Page, PageLoadErrorState> PageLoadErrors = new();

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

    public static void BeginPageLoad(this Page page, Func<Task> loadAsync)
    {
        var pageName = page.GetType().Name;
        var firstLayout = GetFirstLayoutTask(page, pageName);
        Trace.WriteLine($"[Performance][{pageName}] Page entered; data load queued after first layout.");
        _ = RunAfterFirstLayoutSafelyAsync(page, pageName, firstLayout, loadAsync);
    }

    private static async Task RunAfterFirstLayoutSafelyAsync(
        Page page,
        string pageName,
        Task firstLayout,
        Func<Task> loadAsync)
    {
        await firstLayout.ConfigureAwait(false);
        // Defer rendering by 50ms so native Shell slide animation completes without UI contention.
        await Task.Delay(PostLayoutRenderDelayMilliseconds).ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => RunSafelyAsync(page, pageName, "data load", loadAsync));
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
        // SizeChanged can run before the compositor presents the first frame.
        // Defer rendering by 50ms before inflating deferred XAML on the UI thread.
        await Task.Delay(PostLayoutRenderDelayMilliseconds).ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => RunSafelyAsync(page, pageName, "deferred view load", loadAsync));
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

        var message =
            $"[Performance][{component}] {operation} completed in {elapsedMilliseconds:F1} ms ({budgetState}).";
        Trace.WriteLine(message);
        if (ConsoleProfilingEnabled)
            Console.WriteLine(message);
    }

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

    public static bool TryCloseModalOnBack(bool isModalVisible, ICommand closeCommand)
    {
        if (!isModalVisible)
            return false;

        closeCommand.Execute(null);
        return true;
    }

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

    public static void ReattachPropertyChanged(
        this INotifyPropertyChanged viewModel,
        PropertyChangedEventHandler handler)
    {
        viewModel.PropertyChanged -= handler;
        viewModel.PropertyChanged += handler;
    }

    public static bool IsDarkTheme()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }

    public static ThemeChangeObserver CreateThemeObserver(this Page page, Action<bool> refreshThemeColors)
    {
        return new ThemeChangeObserver(theme => refreshThemeColors(theme == AppTheme.Dark));
    }

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

    private sealed class FirstLayoutTracker
    {
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsSubscribed { get; set; }
        public long Started { get; set; }
    }

    private sealed class LazyViewLoadState
    {
        public Task? Task { get; set; }
    }

    private sealed class PageLoadErrorState
    {
        public bool DataFailed { get; set; }
        public bool DeferredFailed { get; set; }
    }
}
