using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Controls.Injury;

/// <summary>
///     Keeps one injury log control subscribed to exactly one view model instance. Both controls relied on the same
///     order: detach the previous instance before attaching the current one, never attach the same instance twice, and
///     do nothing when there is no instance to detach.
/// </summary>
internal sealed class InjuryLogSubscription(PropertyChangedEventHandler handler)
{
    private InjuryLogViewModel? _current;

    public void Attach(object? bindingContext)
    {
        if (bindingContext is not InjuryLogViewModel vm || ReferenceEquals(_current, vm))
            return;

        Detach();
        _current = vm;
        vm.PropertyChanged += handler;
    }

    public void Detach()
    {
        if (_current is null)
            return;

        _current.PropertyChanged -= handler;
        _current = null;
    }
}
