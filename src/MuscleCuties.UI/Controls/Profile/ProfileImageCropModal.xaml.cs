using System.Runtime.CompilerServices;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UI.Services.Profile;

namespace MuscleCuties.UI.Controls.Profile;

public partial class ProfileImageCropModal : ContentView
{
    private double _currentScale;
    private bool _hasContent;
    private double _panX;
    private double _panY;
    private double _startScale;
    private TaskCompletionSource<string?>? _tcs;

    public ProfileImageCropModal()
    {
        _currentScale = AppThemeResources.GetDouble("UiScaleCropMinimum");
        _startScale = _currentScale;
        IsVisible = false;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (_hasContent)
            BottomSheetSizing.Apply(SheetSurface, height);
    }

    public Task<string?> ShowAsync(string imagePath)
    {
        if (!_hasContent)
        {
            InitializeComponent();
            _hasContent = true;
        }

        _tcs = new TaskCompletionSource<string?>();
        CropImage.Source = ImageSource.FromFile(imagePath);
        ResetTransforms();
        BottomSheetSizing.Apply(SheetSurface, Height);
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
                _currentScale = Math.Clamp(_startScale * e.Scale,
                    AppThemeResources.GetDouble("UiScaleCropMinimum"),
                    AppThemeResources.GetDouble("UiScaleCropMaximum"));
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

            await using var stream = await result.OpenReadAsync();
            var path = await ProfileImagePicker.StoreAsync(stream, "profile", ".png");

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
        _panX = AppThemeResources.GetDouble("UiMotionOffsetZero");
        _panY = AppThemeResources.GetDouble("UiMotionOffsetZero");
        _currentScale = AppThemeResources.GetDouble("UiScaleCropMinimum");
        _startScale = _currentScale;
        CropImage.TranslationX = AppThemeResources.GetDouble("UiMotionOffsetZero");
        CropImage.TranslationY = AppThemeResources.GetDouble("UiMotionOffsetZero");
        CropImage.Scale = _currentScale;
    }

    private void ClampTranslation()
    {
        var fallbackImageDimension = AppThemeResources.GetDouble("UiDimension260");
        var viewportSize = CropViewport.Width > 0 ? CropViewport.Width : fallbackImageDimension;
        var imageSize = CropImage.Width > 0 ? CropImage.Width : fallbackImageDimension;
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
