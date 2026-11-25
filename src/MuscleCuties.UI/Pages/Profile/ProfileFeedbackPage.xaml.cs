using MuscleCuties.UI.ViewModels.Profile;

namespace MuscleCuties.UI.Pages.Profile;

/// <summary>
///     Feedback form for questions, bugs, and feature requests.
/// </summary>
public partial class ProfileFeedbackPage : ContentPage
{
    /// <summary>Initializes the page and its view model.</summary>
    public ProfileFeedbackPage(ProfileFeedbackViewModel vm)
    {
        this.InitializeAndBind(InitializeComponent, vm);
    }

    /// <summary>Starts the form when navigated to.</summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        this.BeginPageLoad(() => ((ProfileFeedbackViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    /// <summary>Closes the open sheet before leaving the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ProfileFeedbackViewModel vm && vm.HasUnsavedDraft)
        {
            vm.BackCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>Attaches a file to the feedback message, reporting a picker failure on the page.</summary>
    private async void OnAttachFileClicked(object? sender, EventArgs e)
    {
        var vm = (ProfileFeedbackViewModel)BindingContext;
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = UiTextCatalog.TranslateRuntimeText("Attach feedback context")
            });

            if (result is null)
                return;

            if (string.IsNullOrWhiteSpace(result.FullPath))
            {
                vm.SetAttachmentError("This file cannot be attached from this device.");
                return;
            }

            var fileInfo = new FileInfo(result.FullPath);
            vm.AttachFile(result.FileName, result.FullPath, result.ContentType, fileInfo.Length);
        }
        catch (Exception)
        {
            vm.SetAttachmentError("Could not attach that file. Try a screenshot, PDF, text, or log file.");
        }
    }
}
