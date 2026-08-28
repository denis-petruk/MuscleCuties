using System.IO;
using MuscleCuties.App.Pages;
using MuscleCuties.Core.ViewModels.Profile;

namespace MuscleCuties.App.Pages.Profile;

public partial class ProfileFeedbackPage : ContentPage
{
    public ProfileFeedbackPage(ProfileFeedbackViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.LoadAfterFirstRender(() => ((ProfileFeedbackViewModel)BindingContext).LoadDataCommand.ExecuteAsync(null));
    }

    private async void OnAttachFileClicked(object? sender, EventArgs e)
    {
        var vm = (ProfileFeedbackViewModel)BindingContext;
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Attach feedback context"
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
