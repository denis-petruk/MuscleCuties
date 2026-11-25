using MuscleCuties.UI.ViewModels.Auth;

namespace MuscleCuties.UI.Pages.Auth;

/// <summary>
///     Email verification page that submits the one-time code issued at registration or recovery.
/// </summary>
public partial class OtpVerificationPage : ContentPage
{
    private readonly OtpVerificationViewModel _viewModel;

    /// <summary>Initializes the page and its view model.</summary>
    public OtpVerificationPage(OtpVerificationViewModel viewModel)
    {
        var started = Stopwatch.GetTimestamp();
        this.InitializeWithTiming(InitializeComponent);
        _viewModel = viewModel;
        this.BindWithTiming(viewModel, started);
    }

    /// <summary>Starts the view model's work when the page appears.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(75), () => OtpEntry.Focus());
    }

    /// <summary>Pauses the view model when the page goes away.</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
