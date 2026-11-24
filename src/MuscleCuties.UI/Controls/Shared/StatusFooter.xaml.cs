namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// Busy indicator plus announced status line for an action footer. It binds to the inherited binding context, so a
/// page needs <c>IsBusy</c>, <c>StatusMessage</c> and <c>IsStatusError</c> on its view model and nothing else.
/// </summary>
public partial class StatusFooter : ContentView
{
    public StatusFooter()
    {
        InitializeComponent();
    }
}
