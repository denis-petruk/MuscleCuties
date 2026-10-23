using System.Diagnostics;
using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

public partial class OtpVerificationPage : ContentPage
{
    private readonly OtpVerificationViewModel _viewModel;

    public OtpVerificationPage(OtpVerificationViewModel viewModel)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = viewModel;
        this.BindWithTiming(viewModel, started);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
