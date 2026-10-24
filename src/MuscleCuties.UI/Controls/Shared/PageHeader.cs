using System.Windows.Input;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Controls.Shared;

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

    public PageHeader()
    {
        Rebuild();
    }

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

    private const string AppFontFamilyName = "Inter";
    private const double BackButtonTouchSize = 44;
    private const double BackButtonPadding = 10;
    private const double HeaderTitleFontSize = 24;
    private const double HeaderSubtitleFontSize = 13;
    private const double HeaderColumnSpacing = 8;
    private const double HeaderRowSpacing = 2;

    private void Rebuild()
    {
        var back = new ImageButton
        {
            Source = ImageSource.FromFile("nav_back.png"),
            BackgroundColor = Colors.Transparent,
            WidthRequest = BackButtonTouchSize,
            HeightRequest = BackButtonTouchSize,
            MinimumWidthRequest = BackButtonTouchSize,
            MinimumHeightRequest = BackButtonTouchSize,
            Padding = BackButtonPadding,
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Start,
            Command = new Command(() => BackCommand?.Execute(null))
        };
        SemanticProperties.SetDescription(back, UiTextCatalog.Translate("Back"));

        var title = new Label
        {
            Text = Title,
            FontFamily = AppFontFamilyName,
            FontSize = HeaderTitleFontSize,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        title.SetDynamicResource(StyleProperty, "SubHeadline");

        var header = new Grid { ColumnSpacing = HeaderColumnSpacing, RowSpacing = HeaderRowSpacing };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(BackButtonTouchSize) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(back, 0);
        Grid.SetColumn(back, 0);
        header.Children.Add(back);
        Grid.SetRow(title, 0);
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(Subtitle))
        {
            var subtitle = new Label
            {
                Text = Subtitle,
                FontFamily = AppFontFamilyName,
                FontSize = HeaderSubtitleFontSize,
                LineBreakMode = LineBreakMode.WordWrap
            };
            subtitle.SetDynamicResource(StyleProperty, "MutedLabel");
            header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(subtitle, 1);
            Grid.SetColumn(subtitle, 1);
            header.Children.Add(subtitle);
        }

        Content = header;
    }
}
