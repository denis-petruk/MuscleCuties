using Microsoft.Maui.Controls;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Shared;

public class ThemeAwareImage : Image
{
    public static readonly BindableProperty LightSourceProperty = BindableProperty.Create(
        nameof(LightSource),
        typeof(string),
        typeof(ThemeAwareImage),
        propertyChanged: OnThemeSourceChanged);

    public static readonly BindableProperty DarkSourceProperty = BindableProperty.Create(
        nameof(DarkSource),
        typeof(string),
        typeof(ThemeAwareImage),
        propertyChanged: OnThemeSourceChanged);

    private readonly ThemeChangeObserver _themeObserver;

    public ThemeAwareImage()
    {
        BackgroundColor = AppThemeResources.GetRequired<Color>("Transparent");
        _themeObserver = new ThemeChangeObserver(theme => ApplyThemeImage(ResolveTheme(theme)));
    }

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

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is null)
        {
            _themeObserver.Detach();
            return;
        }

        _themeObserver.Attach();
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

        void Apply()
        {
            Source = ImageSource.FromFile(image);
        }

        if (MainThread.IsMainThread)
            Apply();
        else
            MainThread.BeginInvokeOnMainThread(Apply);
    }

    private static AppTheme ResolveTheme(AppTheme requestedTheme)
    {
        if (requestedTheme is AppTheme.Light or AppTheme.Dark)
            return requestedTheme;

        return Application.Current?.PlatformAppTheme is AppTheme.Light or AppTheme.Dark
            ? Application.Current.PlatformAppTheme
            : AppTheme.Light;
    }
}
