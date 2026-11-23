using System.ComponentModel;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Keeps one control subscribed to exactly one view model of <typeparamref name="TViewModel" />: detach before
///     attach, never subscribe the same instance twice, notify only while attached, and run the owner's attach and
///     teardown hooks with the subscription so no control keeps that bookkeeping itself.
/// </summary>
public sealed class ViewModelSubscription<TViewModel>
    where TViewModel : class, INotifyPropertyChanged
{
    private readonly PropertyChangedEventHandler _onPropertyChanged;
    private readonly Action? _onAttached;
    private readonly Action? _onDetached;
    private TViewModel? _current;

    public ViewModelSubscription(
        PropertyChangedEventHandler onPropertyChanged,
        Action? onAttached = null,
        Action? onDetached = null)
    {
        ArgumentNullException.ThrowIfNull(onPropertyChanged);
        _onPropertyChanged = onPropertyChanged;
        _onAttached = onAttached;
        _onDetached = onDetached;
    }

    public TViewModel? Current => _current;

    /// <summary>
    ///     Subscribes to <paramref name="bindingContext" /> when it is the expected view model. A different view model
    ///     replaces the current one; a non-matching context or a null one detaches, exactly as the controls' own
    ///     detach-then-attach handlers did.
    /// </summary>
    public void BindTo(object? bindingContext)
    {
        if (bindingContext is TViewModel sameInstance && ReferenceEquals(_current, sameInstance))
            return;

        Detach();

        if (bindingContext is not TViewModel viewModel)
            return;

        _current = viewModel;
        viewModel.PropertyChanged += _onPropertyChanged;
        _onAttached?.Invoke();
    }

    public void Detach()
    {
        if (_current is { } viewModel)
        {
            viewModel.PropertyChanged -= _onPropertyChanged;
            _current = null;
        }

        _onDetached?.Invoke();
    }
}
