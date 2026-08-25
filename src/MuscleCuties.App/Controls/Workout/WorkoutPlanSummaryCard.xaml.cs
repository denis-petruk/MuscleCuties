namespace MuscleCuties.App.Controls.Workout;

public partial class WorkoutPlanSummaryCard : ContentView
{
    public static readonly BindableProperty PlanTitleProperty =
        BindableProperty.Create(nameof(PlanTitle), typeof(string), typeof(WorkoutPlanSummaryCard), string.Empty);

    public static readonly BindableProperty SummaryTextProperty =
        BindableProperty.Create(nameof(SummaryText), typeof(string), typeof(WorkoutPlanSummaryCard), string.Empty);

    public WorkoutPlanSummaryCard()
    {
        InitializeComponent();
    }

    public string PlanTitle
    {
        get => (string)GetValue(PlanTitleProperty);
        set => SetValue(PlanTitleProperty, value);
    }

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }
}
