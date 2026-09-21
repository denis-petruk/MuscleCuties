using System.Runtime.CompilerServices;

namespace MuscleCuties.App.Controls.Shared;

public class LazyView : ContentView
{
    public static readonly BindableProperty ContentTemplateProperty =
        BindableProperty.Create(nameof(ContentTemplate), typeof(DataTemplate), typeof(LazyView));

    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (Content is not null)
            Content.BindingContext = BindingContext;
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsVisibleProperty.PropertyName && IsVisible && Content is null)
            LoadContent();
    }

    private void LoadContent()
    {
        if (ContentTemplate is null)
            return;

        Content = (View)ContentTemplate.CreateContent();
        Content.BindingContext = BindingContext;
    }
}
