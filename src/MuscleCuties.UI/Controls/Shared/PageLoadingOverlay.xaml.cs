using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Common;

namespace MuscleCuties.UI.Controls.Shared;

public partial class PageLoadingOverlay : ContentView
{
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

        UpdateVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(IPageLoadAware.IsPageLoading) or nameof(IPageLoadAware.IsLoadError))
            UpdateVisibility();
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

    private void ApplyVisibility() =>
        IsVisible = BindingContext is IPageLoadAware page && (page.IsPageLoading || page.IsLoadError);
}
