using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Controls.Injury;

public partial class InjuryLogContent : ContentView
{
    private readonly InjuryLogSubscription _subscription;

    public InjuryLogContent()
    {
        InitializeComponent();
        _subscription = new InjuryLogSubscription(this, OnViewModelPropertyChanged);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InjuryLogViewModel.ShowAddForm)
            or nameof(InjuryLogViewModel.ActiveInjuries)
            or nameof(InjuryLogViewModel.ShowEmptyState)
            or nameof(InjuryLogViewModel.EmptyStateTitle)
            or nameof(InjuryLogViewModel.EmptyStateDetail))
            Dispatcher.Dispatch(() =>
            {
                InjuryList.InvalidateMeasure();
            });
    }
}
