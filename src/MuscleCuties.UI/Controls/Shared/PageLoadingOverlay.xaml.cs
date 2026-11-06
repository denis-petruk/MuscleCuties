using System.ComponentModel;
using System.Windows.Input;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UI.ViewModels.Common;

namespace MuscleCuties.UI.Controls.Shared;

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
        RetryCommand ?? (BindingContext as IPageLoadAware)?.LoadDataCommand;

    public bool HasRetryCommand => EffectiveRetryCommand is not null;

    private const string DefaultErrorMessageKey = "Dashboard_Shared_CouldNotLoadData";

    private INotifyPropertyChanged? _observedViewModel;

    public PageLoadingOverlay()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
        UpdateVisibility();
    }

    private void OnBindingContextChanged(object? sender, EventArgs args)
    {
        if (_observedViewModel is not null)
            _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _observedViewModel = BindingContext as INotifyPropertyChanged;
        if (_observedViewModel is not null)
            _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;

        NotifyErrorSurfaceChanged();
        UpdateVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(IPageLoadAware.IsPageLoading) or nameof(IPageLoadAware.IsLoadError))
            UpdateVisibility();
    }

    private void NotifyErrorSurfaceChanged()
    {
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(EffectiveRetryCommand));
        OnPropertyChanged(nameof(HasRetryCommand));
    }

    private void UpdateVisibility()
    {
        if (MainThread.IsMainThread)
        {
            ApplyVisibility();
            return;
        }

        MainThread.BeginInvokeOnMainThread(ApplyVisibility);
    }

    private void ApplyVisibility()
    {
        var isVisible = BindingContext is IPageLoadAware page && (page.IsPageLoading || page.IsLoadError);
        IsVisible = isVisible;
        InputTransparent = !isVisible;
    }
}
