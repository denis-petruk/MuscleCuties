using System.ComponentModel;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Controls.Injury;

/// <summary>
///     Body of the injury log: the saved injuries with their delete action.
/// </summary>
public partial class InjuryLogContent : ContentView
{

    /// <summary>Binds the injury list to its view model.</summary>
    public InjuryLogContent()
    {
        InitializeComponent();
        _ = ViewModelSubscriptionBinding.BindToControl<InjuryLogViewModel>(this,
            OnViewModelPropertyChanged);
    }

    /// <summary>Redraws the list when the injury state changes.</summary>
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
