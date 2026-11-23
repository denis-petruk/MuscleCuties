using MuscleCuties.UI.ViewModels.Workout;

namespace MuscleCuties.UI.Services.Workout;

/// <summary>
/// The one place that opens and closes the injury log modal. The dashboard and the workout screen each carried
/// the same reset/show/load sequence, so the modal could drift between the two entry points.
/// </summary>
public static class InjuryModalPresenter
{
    public static async Task OpenAsync(InjuryLogViewModel viewModel, Action<bool> setVisible)
    {
        viewModel.ResetForm();
        setVisible(true);

        if (!viewModel.IsBusy)
            await viewModel.LoadCommand.ExecuteAsync(null);
    }

    public static void Close(InjuryLogViewModel? viewModel, Action<bool> setVisible)
    {
        viewModel?.ResetForm();
        setVisible(false);
    }
}
