using System.Windows.Input;

namespace MuscleCuties.UI.Controls.Shared;

public partial class ModalSheetHeader : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(ModalSheetHeader), string.Empty);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(ModalSheetHeader), string.Empty,
        propertyChanged: static (bindable, _, _) => ((ModalSheetHeader)bindable).OnPropertyChanged(nameof(HasSubtitle)));

    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(
        nameof(BackCommand), typeof(ICommand), typeof(ModalSheetHeader));

    public static readonly BindableProperty ShowBackProperty = BindableProperty.Create(
        nameof(ShowBack), typeof(bool), typeof(ModalSheetHeader), false);

    public static readonly BindableProperty CloseCommandProperty = BindableProperty.Create(
        nameof(CloseCommand), typeof(ICommand), typeof(ModalSheetHeader));

    public static readonly BindableProperty BackDescriptionProperty = BindableProperty.Create(
        nameof(BackDescription), typeof(string), typeof(ModalSheetHeader), string.Empty);

    public static readonly BindableProperty CloseDescriptionProperty = BindableProperty.Create(
        nameof(CloseDescription), typeof(string), typeof(ModalSheetHeader), string.Empty);

    public ModalSheetHeader() => InitializeComponent();

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

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

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

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public string BackDescription
    {
        get => (string)GetValue(BackDescriptionProperty);
        set => SetValue(BackDescriptionProperty, value);
    }

    public string CloseDescription
    {
        get => (string)GetValue(CloseDescriptionProperty);
        set => SetValue(CloseDescriptionProperty, value);
    }
}
