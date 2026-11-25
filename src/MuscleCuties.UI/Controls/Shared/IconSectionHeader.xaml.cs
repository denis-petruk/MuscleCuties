namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
///     Section header with an icon, a title, and an optional trailing action.
/// </summary>
public partial class IconSectionHeader : ContentView
{
    public static readonly BindableProperty IconGlyphProperty = BindableProperty.Create(
        nameof(IconGlyph),
        typeof(string),
        typeof(IconSectionHeader),
        string.Empty);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(IconSectionHeader),
        string.Empty);

    /// <summary>Initializes the header declared in its XAML.</summary>
    public IconSectionHeader()
    {
        InitializeComponent();
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
