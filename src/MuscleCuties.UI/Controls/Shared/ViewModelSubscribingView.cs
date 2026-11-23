using System.ComponentModel;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Keeps a control subscribed to exactly one binding-context view model. Subclasses that set
///     <see cref="ViewModelPropertyChanged" /> get the detach-before-attach ordering, the same-instance guard and the
///     handler-null detach that the injury log controls each carried privately; subclasses that leave it null (every
///     other modal) subscribe to nothing and pay nothing.
/// </summary>
public abstract class ViewModelSubscribingView : ContentView
{
    private INotifyPropertyChanged? _subscribedViewModel;

    /// <summary>Notification handler for the subscribed view model; null disables subscription entirely.</summary>
    protected PropertyChangedEventHandler? ViewModelPropertyChanged { get; set; }

    /// <summary>Optional binding-context type filter; null accepts any observable binding context.</summary>
    protected Type? ViewModelSubscriptionType { get; set; }

    protected override void OnBindingContextChanged()
    {
        DetachViewModel();
        base.OnBindingContextChanged();

        AttachViewModel();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
            DetachViewModel();
        else
            AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (ViewModelPropertyChanged is not { } handler
            || BindingContext is not INotifyPropertyChanged viewModel
            || (ViewModelSubscriptionType is { } type && !type.IsInstanceOfType(viewModel))
            || ReferenceEquals(_subscribedViewModel, viewModel))
            return;

        DetachViewModel();
        _subscribedViewModel = viewModel;
        viewModel.PropertyChanged += handler;
    }

    private void DetachViewModel()
    {
        if (_subscribedViewModel is { } viewModel && ViewModelPropertyChanged is { } handler)
            viewModel.PropertyChanged -= handler;

        _subscribedViewModel = null;
    }
}
