using System.ComponentModel;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Connects a <see cref="ViewModelSubscription{TViewModel}" /> to a control's own lifecycle events, so controls
///     declare no OnBindingContextChanged/OnHandlerChanged overrides and no manual attach/detach code.
/// </summary>
public static class ViewModelSubscriptionBinding
{
    /// <summary>Keeps the subscription following the control's binding context.</summary>
    public static ViewModelSubscription<TViewModel> BindToControl<TViewModel>(
        VisualElement element,
        PropertyChangedEventHandler onPropertyChanged,
        Action? onAttached = null,
        Action? onDetached = null)
        where TViewModel : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(element);

        var subscription = new ViewModelSubscription<TViewModel>(onPropertyChanged, onAttached, onDetached);

        // BindingContextChanged fires after the context is replaced, so binding here keeps the original
        // detach-before-attach order without a pre-detach step.
        element.BindingContextChanged += (_, _) => subscription.BindTo(element.BindingContext);
        element.HandlerChanged += (_, _) =>
        {
            if (element.Handler is null)
                subscription.Detach();
            else
                subscription.BindTo(element.BindingContext);
        };

        return subscription;
    }
}
