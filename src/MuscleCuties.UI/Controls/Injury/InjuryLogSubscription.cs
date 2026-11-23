using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Controls.Injury;

/// <summary>
///     Keeps one injury log control subscribed to exactly one view model instance, and owns the lifecycle wiring both
///     controls used to repeat: detach the previous instance before attaching the current one, never attach the same
///     instance twice, and do nothing when there is no instance to detach. Survives rebinding and handler recreation.
/// </summary>
internal sealed class InjuryLogSubscription
{
    private readonly PropertyChangedEventHandler _handler;
    private InjuryLogViewModel? _current;

    public InjuryLogSubscription(VisualElement element, PropertyChangedEventHandler handler)
    {
        _handler = handler;
        element.BindingContextChanged += OnBindingContextChanged;
        element.HandlerChanged += OnHandlerChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        Detach();

        if (sender is VisualElement element)
            Attach(element.BindingContext);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is not VisualElement element)
            return;

        if (element.Handler is null)
            Detach();
        else
            Attach(element.BindingContext);
    }

    private void Attach(object? bindingContext)
    {
        if (bindingContext is not InjuryLogViewModel viewModel || ReferenceEquals(_current, viewModel))
            return;

        Detach();
        _current = viewModel;
        viewModel.PropertyChanged += _handler;
    }

    private void Detach()
    {
        if (_current is null)
            return;

        _current.PropertyChanged -= _handler;
        _current = null;
    }
}
