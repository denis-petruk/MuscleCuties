using Microsoft.Maui.ApplicationModel;

namespace MuscleCuties.App.Controls.Shared;

public class ThemeAwareImage : Image
{
    public static readonly BindableProperty LightSourceProperty = BindableProperty.Create(
        nameof(LightSource),
        typeof(string),
        typeof(ThemeAwareImage),
        default(string),
        propertyChanged: OnThemeSourceChanged);

    public static readonly BindableProperty DarkSourceProperty = BindableProperty.Create(
        nameof(DarkSource),
        typeof(string),
        typeof(ThemeAwareImage),
        default(string),
        propertyChanged: OnThemeSourceChanged);

    private bool _isThemeHandlerAttached;

    public string? LightSource
    {
        get => (string?)GetValue(LightSourceProperty);
        set => SetValue(LightSourceProperty, value);
    }

    public string? DarkSource
    {
        get => (string?)GetValue(DarkSourceProperty);
        set => SetValue(DarkSourceProperty, value);
    }

    public ThemeAwareImage()
    {
        BackgroundColor = Colors.Transparent;
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is null)
        {
            DetachThemeHandler();
            return;
        }

        AttachThemeHandler();
        ApplyThemeImage();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        ApplyThemeImage();
    }

    private static void OnThemeSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((ThemeAwareImage)bindable).ApplyThemeImage();
    }

    private void AttachThemeHandler()
    {
        if (_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        _isThemeHandlerAttached = true;
    }

    private void DetachThemeHandler()
    {
        if (!_isThemeHandlerAttached || Application.Current is null)
            return;

        Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        _isThemeHandlerAttached = false;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        ApplyThemeImage(ResolveTheme(e.RequestedTheme));
    }

    protected void ApplyThemeImage()
    {
        ApplyThemeImage(ResolveTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified));
    }

    private void ApplyThemeImage(AppTheme theme)
    {
        var image = theme == AppTheme.Dark
            ? DarkSource ?? LightSource
            : LightSource ?? DarkSource;

        if (string.IsNullOrWhiteSpace(image))
            return;

        void Apply() => Source = ImageSource.FromFile(image);

        if (MainThread.IsMainThread)
            Apply();
        else
            MainThread.BeginInvokeOnMainThread(Apply);
    }

    private static AppTheme ResolveTheme(AppTheme theme)
    {
        if (theme != AppTheme.Unspecified)
            return theme;

        var appInfoTheme = AppInfo.RequestedTheme;
        return appInfoTheme == AppTheme.Unspecified ? AppTheme.Light : appInfoTheme;
    }
}
