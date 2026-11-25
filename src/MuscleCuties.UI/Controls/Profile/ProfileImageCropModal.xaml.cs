using System.Windows.Input;

namespace MuscleCuties.UI.Controls.Profile;

/// <summary>
///     Crop step for a chosen profile photo; returns the stored path, or null when cancelled.
/// </summary>
public partial class ProfileImageCropModal : AnimatedModalView
{
    private double _currentScale;
    private bool _hasContent;
    private bool _isSaving;
    private double _panX;
    private double _panY;
    private double _startScale;
    private TaskCompletionSource<string?>? _tcs;

    /// <summary>Wires the crop surface's gestures, so the image can be moved and zoomed before it is cut.</summary>
    public ProfileImageCropModal()
    {
        _currentScale = AppThemeResources.GetDouble("UiScaleCropMinimum");
        _startScale = _currentScale;
        CancelCommand = new RelayCommand(() => OnCancelTapped(this, EventArgs.Empty));
        BindingContext = this;
    }

    /// <summary>The command that closes the crop modal without saving.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Fits the crop sheet's height to its content.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (_hasContent)
            // Content-fit: the crop frame and one hint line do not need the full sheet ceiling.
            BottomSheetSizing.ApplyContentFit(SheetSurface, height);
    }

    /// <summary>Opens the sheet on an image and completes with the cropped file's path, or nothing when the user backs</summary>
    /// out.</summary>
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
        BottomSheetSizing.ApplyContentFit(SheetSurface, Height);
        IsOpen = true;
        return _tcs.Task;
    }

    /// <summary>Drags the image, then clamps it so no empty edge can appear inside the crop frame.</summary>
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

    /// <summary>Zooms about the pinch's own origin and clamps the result, so zooming cannot slide the image out of the</summary>
    /// frame.</summary>
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

    /// <summary>Cuts the visible frame to a file and completes the sheet with it; a failure ends the sheet with nothing rather</summary>
    /// than a broken path.</summary>
    private async void OnUsePhotoTapped(object? sender, EventArgs e)
    {
        if (_isSaving)
            return;

        _isSaving = true;
        UsePhotoButton.IsEnabled = false;
        try
        {
            var result = await CropViewport.CaptureAsync();
            if (result is null)
            {
                _tcs?.TrySetResult(null);
                return;
            }

            await using var stream = await result.OpenReadAsync();
            var path = await ProfileImagePicker.StoreAsync(stream, "profile", ".png");

            _tcs?.TrySetResult(path);
        }
        catch (Exception ex)
        {
            _tcs?.TrySetException(ex);
        }
        finally
        {
            _isSaving = false;
            UsePhotoButton.IsEnabled = true;
            Hide();
        }
    }

    /// <summary>Closes the sheet without a result, leaving the previous avatar untouched.</summary>
    private void OnCancelTapped(object? sender, EventArgs e)
    {
        _tcs?.TrySetResult(null);
        Hide();
    }

    /// <summary>Hides the sheet and restores the image's starting transform, so the next photo does not open zoomed or</summary>
    /// shifted.</summary>
    private void Hide()
    {
        IsOpen = false;
    }

    /// <summary>Returns the image to its fitted position and scale.</summary>
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

    /// <summary>Keeps the image covering the frame: it can be moved only as far as its own edges allow.</summary>
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

}
