using System.Windows.Input;

namespace MuscleCuties.App.Controls.Shared;

public class PageHeader : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(PageHeader), string.Empty,
            propertyChanged: (b, _, _) => ((PageHeader)b).Rebuild());

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty,
            propertyChanged: (b, _, _) => ((PageHeader)b).Rebuild());

    public static readonly BindableProperty BackCommandProperty =
        BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(PageHeader));

    public PageHeader() => Rebuild();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    private void Rebuild()
    {
        var back = new Label
        {
            Text = "\u2190",
            FontSize = 22,
            VerticalOptions = LayoutOptions.Center
        };
        back.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => BackCommand?.Execute(null))
        });

        var title = new Label
        {
            Text = Title,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };

        var header = new HorizontalStackLayout { Spacing = 12 };
        header.Children.Add(back);
        header.Children.Add(title);

        var layout = new VerticalStackLayout { Spacing = 4 };
        layout.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(Subtitle))
        {
            layout.Children.Add(new Label
            {
                Text = Subtitle,
                FontSize = 13,
                Opacity = 0.6
            });
        }

        Content = layout;
    }
}
