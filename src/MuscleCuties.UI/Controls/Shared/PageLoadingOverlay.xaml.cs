using System.ComponentModel;
using System.Windows.Input;
using MuscleCuties.UI.ViewModels.Common;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>The full-page overlay: a spinner while a page loads and an error surface with its retry when the load</summary>
/// fails.</summary>
public partial class PageLoadingOverlay : ContentView
{
    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(
        nameof(BackCommand), typeof(ICommand), typeof(PageLoadingOverlay),
        propertyChanged: (bindable, _, _) => ((PageLoadingOverlay)bindable).OnPropertyChanged(nameof(HasBackCommand)));

    /// <summary>
    /// Optional page-specific failure copy. When it is absent the shared generic line is used, so
    /// pages that do not classify their load failure keep the existing appearance.
    /// </summary>
    public static readonly BindableProperty MessageTextProperty = BindableProperty.Create(
        nameof(MessageText), typeof(string), typeof(PageLoadingOverlay), default(string),
        propertyChanged: (bindable, _, _) => ((PageLoadingOverlay)bindable).NotifyErrorSurfaceChanged());

    /// <summary>
    /// Optional retry command. A page that classifies its failure supplies a command that is absent
    /// when repeating the request cannot succeed, instead of an unconditional retry affordance.
    /// </summary>
    public static readonly BindableProperty RetryCommandProperty = BindableProperty.Create(
        nameof(RetryCommand), typeof(ICommand), typeof(PageLoadingOverlay), default(ICommand),
        propertyChanged: (bindable, _, _) => ((PageLoadingOverlay)bindable).NotifyErrorSurfaceChanged());

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    /// <summary>True when the overlay has a back command.</summary>
    public bool HasBackCommand => BackCommand is not null;

    public string? MessageText
    {
        get => (string?)GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    /// <summary>The failure copy actually shown: the page's classified message, or the shared line.</summary>
    public string ErrorMessage => string.IsNullOrWhiteSpace(MessageText)
        ? UiTextCatalog.Get(DefaultErrorMessageKey)
        : MessageText!;

    /// <summary>The retry the page allows, or null when no retry can succeed.</summary>
    public ICommand? EffectiveRetryCommand =>
        IsSet(RetryCommandProperty)
            ? RetryCommand
            : (BindingContext as IPageLoadAware)?.LoadDataCommand;

    /// <summary>True when the overlay can retry.</summary>
    public bool HasRetryCommand => EffectiveRetryCommand is not null;

    private const string DefaultErrorMessageKey = "Dashboard_Shared_CouldNotLoadData";

    private readonly ViewModelSubscription<INotifyPropertyChanged> _subscription;

    /// <summary>Initializes the overlay and follows its view model's loading state.</summary>
    public PageLoadingOverlay()
    {
        InitializeComponent();
        _subscription = ViewModelSubscriptionBinding.BindToControl<INotifyPropertyChanged>(this,
            OnViewModelPropertyChanged);
        BindingContextChanged += (_, _) =>
        {
            NotifyErrorSurfaceChanged();
            UpdateVisibility();
        };
        UpdateVisibility();
    }

    /// <summary>Shows or hides the overlay when the load state changes.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(IPageLoadAware.IsPageLoading) or nameof(IPageLoadAware.IsLoadError))
            UpdateVisibility();
    }

    /// <summary>Republishes the error surface after its content changed, so the overlay can size it to the retry it now</summary>
    /// holds.</summary>
    private void NotifyErrorSurfaceChanged()
    {
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(EffectiveRetryCommand));
        OnPropertyChanged(nameof(HasRetryCommand));
    }

    /// <summary>Chooses between the spinner, the error surface and nothing, from the page's own load state.</summary>
    private void UpdateVisibility()
    {
        if (MainThread.IsMainThread)
        {
            ApplyVisibility();
            return;
        }

        MainThread.BeginInvokeOnMainThread(ApplyVisibility);
    }

    /// <summary>Applies that choice to the overlay with a fade, so the overlay does not flicker on a fast load.</summary>
    private void ApplyVisibility()
    {
        var isVisible = BindingContext is IPageLoadAware page && (page.IsPageLoading || page.IsLoadError);
        IsVisible = isVisible;
        InputTransparent = !isVisible;
    }
}
