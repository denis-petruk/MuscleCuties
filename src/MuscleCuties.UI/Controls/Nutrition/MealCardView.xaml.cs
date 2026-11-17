using System.Windows.Input;

namespace MuscleCuties.UI.Controls.Nutrition;

public partial class MealCardView : ContentView
{
    public MealCardView()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(
        nameof(Heading), typeof(string), typeof(MealCardView), string.Empty);

    public static readonly BindableProperty CaloriesTextProperty = BindableProperty.Create(
        nameof(CaloriesText), typeof(string), typeof(MealCardView), string.Empty);

    public static readonly BindableProperty TargetShareTextProperty = BindableProperty.Create(
        nameof(TargetShareText), typeof(string), typeof(MealCardView), string.Empty,
        propertyChanged: (bindable, _, _) =>
            ((MealCardView)bindable).OnPropertyChanged(nameof(HasTargetShare)));

    public static readonly BindableProperty MealNameProperty = BindableProperty.Create(
        nameof(MealName), typeof(string), typeof(MealCardView), string.Empty,
        propertyChanged: (bindable, _, _) =>
            ((MealCardView)bindable).OnPropertyChanged(nameof(HasMealName)));

    public static readonly BindableProperty IngredientsTextProperty = BindableProperty.Create(
        nameof(IngredientsText), typeof(string), typeof(MealCardView), string.Empty,
        propertyChanged: (bindable, _, _) =>
            ((MealCardView)bindable).OnPropertyChanged(nameof(HasIngredients)));

    public static readonly BindableProperty MacrosTextProperty = BindableProperty.Create(
        nameof(MacrosText), typeof(string), typeof(MealCardView), string.Empty);

    public static readonly BindableProperty MealTypeIconDataProperty = BindableProperty.Create(
        nameof(MealTypeIconData), typeof(string), typeof(MealCardView), string.Empty);

    public static readonly BindableProperty SemanticDescriptionProperty = BindableProperty.Create(
        nameof(SemanticDescription), typeof(string), typeof(MealCardView), string.Empty);

    public static readonly BindableProperty HasContentProperty = BindableProperty.Create(
        nameof(HasContent), typeof(bool), typeof(MealCardView), true,
        propertyChanged: (bindable, _, _) =>
            ((MealCardView)bindable).OnPropertyChanged(nameof(IsEmpty)));

    public static readonly BindableProperty ShowEditProperty = BindableProperty.Create(
        nameof(ShowEdit), typeof(bool), typeof(MealCardView), true,
        propertyChanged: (bindable, _, _) =>
            ((MealCardView)bindable).OnPropertyChanged(nameof(HasActions)));

    public static readonly BindableProperty ShowTemplateProperty = BindableProperty.Create(
        nameof(ShowTemplate), typeof(bool), typeof(MealCardView), true,
        propertyChanged: (bindable, _, _) =>
            ((MealCardView)bindable).OnPropertyChanged(nameof(HasActions)));

    public static readonly BindableProperty EditCommandProperty = BindableProperty.Create(
        nameof(EditCommand), typeof(ICommand), typeof(MealCardView));

    public static readonly BindableProperty EditCommandParameterProperty = BindableProperty.Create(
        nameof(EditCommandParameter), typeof(object), typeof(MealCardView));

    public static readonly BindableProperty TemplateCommandProperty = BindableProperty.Create(
        nameof(TemplateCommand), typeof(ICommand), typeof(MealCardView));

    public static readonly BindableProperty TemplateCommandParameterProperty = BindableProperty.Create(
        nameof(TemplateCommandParameter), typeof(object), typeof(MealCardView));

    public static readonly BindableProperty OpenCommandProperty = BindableProperty.Create(
        nameof(OpenCommand), typeof(ICommand), typeof(MealCardView));

    public static readonly BindableProperty OpenCommandParameterProperty = BindableProperty.Create(
        nameof(OpenCommandParameter), typeof(object), typeof(MealCardView));

    public static readonly BindableProperty BelowContentProperty = BindableProperty.Create(
        nameof(BelowContent), typeof(View), typeof(MealCardView));

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string CaloriesText
    {
        get => (string)GetValue(CaloriesTextProperty);
        set => SetValue(CaloriesTextProperty, value);
    }

    public string TargetShareText
    {
        get => (string)GetValue(TargetShareTextProperty);
        set => SetValue(TargetShareTextProperty, value);
    }

    public string MealName
    {
        get => (string)GetValue(MealNameProperty);
        set => SetValue(MealNameProperty, value);
    }

    public string IngredientsText
    {
        get => (string)GetValue(IngredientsTextProperty);
        set => SetValue(IngredientsTextProperty, value);
    }

    public string MacrosText
    {
        get => (string)GetValue(MacrosTextProperty);
        set => SetValue(MacrosTextProperty, value);
    }

    public string MealTypeIconData
    {
        get => (string)GetValue(MealTypeIconDataProperty);
        set => SetValue(MealTypeIconDataProperty, value);
    }

    public string SemanticDescription
    {
        get => (string)GetValue(SemanticDescriptionProperty);
        set => SetValue(SemanticDescriptionProperty, value);
    }

    public bool HasContent
    {
        get => (bool)GetValue(HasContentProperty);
        set => SetValue(HasContentProperty, value);
    }

    public bool IsEmpty => !HasContent;

    public bool ShowEdit
    {
        get => (bool)GetValue(ShowEditProperty);
        set => SetValue(ShowEditProperty, value);
    }

    public bool ShowTemplate
    {
        get => (bool)GetValue(ShowTemplateProperty);
        set => SetValue(ShowTemplateProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public object? EditCommandParameter
    {
        get => GetValue(EditCommandParameterProperty);
        set => SetValue(EditCommandParameterProperty, value);
    }

    public ICommand? TemplateCommand
    {
        get => (ICommand?)GetValue(TemplateCommandProperty);
        set => SetValue(TemplateCommandProperty, value);
    }

    public object? TemplateCommandParameter
    {
        get => GetValue(TemplateCommandParameterProperty);
        set => SetValue(TemplateCommandParameterProperty, value);
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public object? OpenCommandParameter
    {
        get => GetValue(OpenCommandParameterProperty);
        set => SetValue(OpenCommandParameterProperty, value);
    }

    public View? BelowContent
    {
        get => (View?)GetValue(BelowContentProperty);
        set => SetValue(BelowContentProperty, value);
    }

    public bool HasActions => ShowEdit || ShowTemplate;
    public bool HasTargetShare => !string.IsNullOrWhiteSpace(TargetShareText);
    public bool HasMealName => !string.IsNullOrWhiteSpace(MealName);
    public bool HasIngredients => !string.IsNullOrWhiteSpace(IngredientsText);
}
