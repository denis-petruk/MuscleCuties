using System.Windows.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.UI.Resources.Styles;
using MuscleCuties.UI.Services.Localization;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// The single page header composition: an optional back affordance, the level-one title, an
/// optional subtitle or status view, and one contextual action. Pages supply content and
/// commands, never their own header grid, so insets, width, heading semantics and the back
/// action's accessible name stay in one place. A root page sets <see cref="ShowBack"/> to false
/// instead of faking a disabled back button. Bound properties update in place; the tree is only
/// rebuilt when <see cref="ShowBack"/> changes shape.
/// </summary>
public class PageHeader : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(PageHeader), string.Empty,
            propertyChanged: (b, _, _) => ((PageHeader)b).ApplyText());

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty,
            propertyChanged: (b, _, _) => ((PageHeader)b).ApplyText());

    public static readonly BindableProperty BackCommandProperty =
        BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(PageHeader),
            propertyChanged: (b, _, _) => ((PageHeader)b).ApplyBackCommand());

    public static readonly BindableProperty ShowBackProperty =
        BindableProperty.Create(nameof(ShowBack), typeof(bool), typeof(PageHeader), true,
            propertyChanged: (b, _, _) => ((PageHeader)b).Build());

    public static readonly BindableProperty ActionContentProperty =
        BindableProperty.Create(nameof(ActionContent), typeof(View), typeof(PageHeader),
            propertyChanged: (b, _, _) => ((PageHeader)b).ApplyAction());

    public static readonly BindableProperty ActionTextProperty =
        BindableProperty.Create(nameof(ActionText), typeof(string), typeof(PageHeader), string.Empty,
            propertyChanged: (b, _, _) => ((PageHeader)b).ApplyAction());

    public static readonly BindableProperty ActionCommandProperty =
        BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(PageHeader),
            propertyChanged: (b, _, _) => ((PageHeader)b).ApplyAction());

    public static readonly BindableProperty StatusContentProperty =
        BindableProperty.Create(nameof(StatusContent), typeof(View), typeof(PageHeader),
            propertyChanged: (b, _, _) => ((PageHeader)b).ApplyStatus());

    private readonly Command _backProxy;
    private Label? _titleLabel;
    private Label? _subtitleLabel;
    private ImageButton? _backButton;
    private ContentView? _actionHost;
    private ContentView? _statusHost;
    private bool _builtWithBack;

    public PageHeader()
    {
        _backProxy = new Command(() => BackCommand?.Execute(null));
        Build();
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

    public bool ShowBack
    {
        get => (bool)GetValue(ShowBackProperty);
        set => SetValue(ShowBackProperty, value);
    }

    /// <summary>
    ///     The page's single contextual action. A page supplies its own already-accessible control
    ///     (for example a shared <c>HeaderIconButton</c> Border) instead of composing a second
    ///     header column beside this one.
    /// </summary>
    public View? ActionContent
    {
        get => (View?)GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    /// <summary>A text action rendered with the shared secondary button style.</summary>
    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>
    ///     A compact status view in the line under the title — a phase badge or a state pill. It
    ///     takes the place of the text subtitle when present, so the two never compete for the
    ///     same line. A page supplies its own already-styled control (for example a PillBadge).
    /// </summary>
    public View? StatusContent
    {
        get => (View?)GetValue(StatusContentProperty);
        set => SetValue(StatusContentProperty, value);
    }

    private void Build()
    {
        // Detach hosted views before replacing the tree so nothing is parented twice.
        if (_actionHost is not null)
            _actionHost.Content = null;
        if (_statusHost is not null)
            _statusHost.Content = null;

        _actionHost = null;
        _statusHost = null;
        _backButton = null;
        _builtWithBack = ShowBack;

        var grid = new Grid
        {
            ColumnSpacing = AppThemeResources.GetDouble("UiSpaceTight"),
            RowSpacing = AppThemeResources.GetDouble("UiSpacing2")
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleColumn = 0;
        if (ShowBack)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(AppThemeResources.GetDouble("UiDimension44"))
            });
            _backButton = CreateBackButton();
            grid.Children.Add(_backButton);
            Grid.SetColumn(_backButton, 0);
            Grid.SetRowSpan(_backButton, 2);
            titleColumn = 1;
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new Label
        {
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        title.SetDynamicResource(StyleProperty, "PageTitle");
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        _titleLabel = title;
        grid.Children.Add(title);
        Grid.SetColumn(title, titleColumn);

        _subtitleLabel = new Label { LineBreakMode = LineBreakMode.WordWrap };
        _subtitleLabel.SetDynamicResource(StyleProperty, "MutedLabel");
        grid.Children.Add(_subtitleLabel);
        Grid.SetColumn(_subtitleLabel, titleColumn);
        Grid.SetRow(_subtitleLabel, 1);

        _statusHost = new ContentView { VerticalOptions = LayoutOptions.Start, IsVisible = false };
        grid.Children.Add(_statusHost);
        Grid.SetColumn(_statusHost, titleColumn);
        Grid.SetRow(_statusHost, 1);

        _actionHost = new ContentView { VerticalOptions = LayoutOptions.Start };
        grid.Children.Add(_actionHost);
        Grid.SetColumn(_actionHost, titleColumn + 1);
        Grid.SetRowSpan(_actionHost, 2);

        Content = grid;
        ApplyText();
        ApplyAction();
        ApplyStatus();
        ApplyBackCommand();
    }

    private ImageButton CreateBackButton()
    {
        var back = new ImageButton
        {
            Source = ImageSource.FromFile("nav_back.png"),
            BackgroundColor = AppThemeResources.GetRequired<Color>("Transparent"),
            WidthRequest = AppThemeResources.GetDouble("PresentationMinimumTouchSize"),
            HeightRequest = AppThemeResources.GetDouble("PresentationMinimumTouchSize"),
            MinimumWidthRequest = AppThemeResources.GetDouble("PresentationMinimumTouchSize"),
            MinimumHeightRequest = AppThemeResources.GetDouble("PresentationMinimumTouchSize"),
            Padding = AppThemeResources.GetThickness("UiInsets10"),
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Start,
            Command = _backProxy
        };
        var backName = UiTextCatalog.TranslateRuntimeText("Back");
        AutomationProperties.SetName(back, backName);
        SemanticProperties.SetDescription(back, backName);
        return back;
    }

    private void ApplyText()
    {
        if (_titleLabel is null)
            return;

        _titleLabel.Text = Title;
        if (_subtitleLabel is null)
            return;

        _subtitleLabel.Text = Subtitle;
        _subtitleLabel.IsVisible = StatusContent is null && !string.IsNullOrWhiteSpace(Subtitle);
    }

    private void ApplyStatus()
    {
        if (_statusHost is null)
            return;

        if (StatusContent is { } custom)
        {
            _statusHost.Content = null;
            _statusHost.Content = custom;
            _statusHost.IsVisible = true;
        }
        else
        {
            _statusHost.Content = null;
            _statusHost.IsVisible = false;
        }

        // The status view and the text subtitle share one line; the status wins when present.
        if (_subtitleLabel is not null)
            _subtitleLabel.IsVisible = StatusContent is null && !string.IsNullOrWhiteSpace(Subtitle);
    }

    private void ApplyBackCommand()
    {
        if (_backButton is not null)
            _backButton.Command = _backProxy;
    }

    private void ApplyAction()
    {
        if (_actionHost is null)
            return;

        if (!string.IsNullOrWhiteSpace(ActionText))
        {
            var button = new Button
            {
                Text = ActionText,
                Command = ActionCommand,
                VerticalOptions = LayoutOptions.Center
            };
            button.SetDynamicResource(StyleProperty, "ButtonSecondary");
            _actionHost.Content = button;
            _actionHost.IsVisible = true;
            return;
        }

        if (ActionContent is { } custom)
        {
            _actionHost.Content = null;
            _actionHost.Content = custom;
            _actionHost.IsVisible = true;
            return;
        }

        _actionHost.Content = null;
        _actionHost.IsVisible = false;
    }
}
