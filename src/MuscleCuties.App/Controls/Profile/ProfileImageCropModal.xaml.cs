using System.Runtime.CompilerServices;
using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Profile;

public partial class ProfileImageCropModal : ContentView
{
    private TaskCompletionSource<string?>? _tcs;
    private double _panX;
    private double _panY;
    private double _currentScale = 1;
    private double _startScale = 1;

    public ProfileImageCropModal()
    {
        InitializeComponent();
    }

    public Task<string?> ShowAsync(string imagePath)
    {
        _tcs = new TaskCompletionSource<string?>();
        CropImage.Source = ImageSource.FromFile(imagePath);
        ResetTransforms();
        IsVisible = true;
        return _tcs.Task;
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                CropImage.TranslationX = _panX + e.TotalX;
                CropImage.TranslationY = _panY + e.TotalY;
                ClampTranslation();
                break;
            case GestureStatus.Completed:
                _panX = CropImage.TranslationX;
                _panY = CropImage.TranslationY;
                break;
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _startScale = _currentScale;
                break;
            case GestureStatus.Running:
                _currentScale = Math.Clamp(_startScale * e.Scale, 1, 5);
                CropImage.Scale = _currentScale;
                ClampTranslation();
                break;
        }
    }

    private async void OnUsePhotoTapped(object? sender, EventArgs e)
    {
        try
        {
            var result = await CropViewport.CaptureAsync();
            if (result is null)
            {
                _tcs?.TrySetResult(null);
                Hide();
                return;
            }

            var folder = Path.Combine(FileSystem.AppDataDirectory, "profile");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "profile_avatar.png");

            await using var stream = await result.OpenReadAsync();
            await using var file = File.Create(path);
            await stream.CopyToAsync(file);

            _tcs?.TrySetResult(path);
        }
        catch
        {
            _tcs?.TrySetResult(null);
        }
        finally
        {
            Hide();
        }
    }

    private void OnCancelTapped(object? sender, EventArgs e)
    {
        _tcs?.TrySetResult(null);
        Hide();
    }

    private void Hide()
    {
        ModalTransition.PlayHide(this, () =>
        {
            IsVisible = false;
            CropImage.Source = null;
        });
    }

    private void ResetTransforms()
    {
        _panX = 0;
        _panY = 0;
        _currentScale = 1;
        _startScale = 1;
        CropImage.TranslationX = 0;
        CropImage.TranslationY = 0;
        CropImage.Scale = 1;
    }

    private void ClampTranslation()
    {
        var viewportSize = CropViewport.Width > 0 ? CropViewport.Width : 260;
        var imageSize = CropImage.Width > 0 ? CropImage.Width : 260;
        var maxOffset = Math.Max(0, (imageSize * _currentScale - viewportSize) / 2);

        CropImage.TranslationX = Math.Clamp(CropImage.TranslationX, -maxOffset, maxOffset);
        CropImage.TranslationY = Math.Clamp(CropImage.TranslationY, -maxOffset, maxOffset);

        _panX = CropImage.TranslationX;
        _panY = CropImage.TranslationY;
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible) && IsVisible)
            ModalTransition.PlayShow(this);
    }
}
