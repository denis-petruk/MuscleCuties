using System.ComponentModel;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Controls.Injury;

public partial class InjuryLogContent : ContentView
{
    private InjuryLogViewModel? _subscribedViewModel;

    public InjuryLogContent()
    {
        InitializeComponent();
    }

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
        if (BindingContext is not InjuryLogViewModel vm || ReferenceEquals(_subscribedViewModel, vm))
            return;

        DetachViewModel();
        _subscribedViewModel = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InjuryLogViewModel.ShowAddForm)
                           or nameof(InjuryLogViewModel.ActiveInjuries)
                           or nameof(InjuryLogViewModel.HasNoInjuries)
                           or nameof(InjuryLogViewModel.HasNoActiveInjuries))
        {
            Dispatcher.Dispatch(() =>
            {
                InjuryList.InvalidateMeasure();
            });
        }
    }
}
