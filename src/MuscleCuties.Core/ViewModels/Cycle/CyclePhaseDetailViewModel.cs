using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.UI.Cycle;

namespace MuscleCuties.Core.ViewModels.Cycle;

public partial class CyclePhaseDetailViewModel : ObservableObject
{
    private readonly Action _navigateBack;

    [ObservableProperty] private CyclePhase _currentPhase = CyclePhase.Follicular;
    [ObservableProperty] private string _phaseName = "Follicular";
    [ObservableProperty] private string _phaseWindow = "Days 6-13";
    [ObservableProperty] private string _headline = "Build momentum";
    [ObservableProperty] private string _hook = "This is usually the easiest time to ask your body for a little more.";
    [ObservableProperty] private string _whyLead = string.Empty;
    [ObservableProperty] private string _nutritionLead = string.Empty;
    [ObservableProperty] private string _workoutLead = string.Empty;
    [ObservableProperty] private string _quickRule = string.Empty;
    [ObservableProperty] private string _phaseIconGlyph = "LeafThree24";
    [ObservableProperty] private string _phaseImageSource = CyclePhaseAssets.FollicularAnimation;
    [ObservableProperty] private bool _phaseImageUsesAnimation = true;
    [ObservableProperty] private double _trainingSignal = 0.7;
    [ObservableProperty] private double _nutritionSignal = 0.7;
    [ObservableProperty] private double _recoverySignal = 0.5;
    [ObservableProperty] private string _trainingSignalText = "Build";
    [ObservableProperty] private string _nutritionSignalText = "Balanced";
    [ObservableProperty] private string _recoverySignalText = "Steady";
    [ObservableProperty] private bool _isWatchExpanded;
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _whyPoints = new();
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _nutritionPoints = new();
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _workoutPoints = new();
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _watchPoints = new();

    public RelayCommand BackCommand { get; }
    public RelayCommand ToggleWatchCommand { get; }
    public string TrainingIconGlyph => "Dumbbell24";
    public string NutritionIconGlyph => "Food24";
    public string RecoveryIconGlyph => "WeatherMoon24";
    public string WatchIconGlyph => "AlertUrgent24";

    public CyclePhaseDetailViewModel(Action navigateBack)
    {
        _navigateBack = navigateBack;
        BackCommand = new RelayCommand(_navigateBack);
        ToggleWatchCommand = new RelayCommand(() => IsWatchExpanded = !IsWatchExpanded);
        Load(CyclePhase.Follicular);
    }

    public void Load(CyclePhase phase)
    {
        var detail = BuildDetail(phase);
        CurrentPhase = phase;
        PhaseName = detail.PhaseName;
        PhaseWindow = detail.PhaseWindow;
        Headline = detail.Headline;
        Hook = detail.Hook;
        WhyLead = detail.WhyLead;
        NutritionLead = detail.NutritionLead;
        WorkoutLead = detail.WorkoutLead;
        QuickRule = detail.QuickRule;
        PhaseIconGlyph = GetPhaseIconGlyph(phase);
        PhaseImageSource = CyclePhaseAssets.GetVisualSource(phase);
        PhaseImageUsesAnimation = CyclePhaseAssets.UsesAnimatedVisual(phase);
        IsWatchExpanded = false;
        ApplySignalModel(phase);
        WhyPoints = BuildPointCollection(detail.WhyPoints);
        NutritionPoints = BuildPointCollection(detail.NutritionPoints);
        WorkoutPoints = BuildPointCollection(detail.WorkoutPoints);
        WatchPoints = BuildPointCollection(detail.WatchPoints);
    }

    private static ObservableCollection<CyclePhaseDetailPoint> BuildPointCollection(
        IReadOnlyList<CyclePhaseDetailPoint> points) =>
        new(points.Select(point => new CyclePhaseDetailPoint
        {
            Title = point.Title,
            Text = point.Text,
            IconGlyph = point.IconGlyph
        }));

    private static CyclePhaseDetailContent BuildDetail(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => new CyclePhaseDetailContent(
            "Menstrual",
            "Usually days 1-5",
            "The reset window",
            "This is not a failed training week. It is the part where your body asks for a cleaner plan.",
            "Hormones are low and cramps can steal output. Tracking this phase stops you confusing lower capacity with lower discipline.",
            "Replace restriction with repair: iron, fluids, steady carbs, and protein that does not require heroic cooking.",
            "Keep the habit alive, lower the noise. Movement should make the day easier, not harder.",
            "If pain is high, train the routine: walk, mobilize, eat, sleep. That still counts.",
            [
                Point("Energy has a real reason to dip", "Lower estrogen and progesterone can make heavy work feel expensive. Plan for that instead of blaming yourself.", "BatteryWarning24"),
                Point("Bleeding changes nutrition math", "Iron and fluid losses are real for some people. Logs help you notice your own pattern.", "Drop24"),
                Point("Recovery gives you better later weeks", "A slightly easier few days often protects follicular and ovulatory training quality.", "ArrowTrending24")
            ],
            [
                Point("Iron plus vitamin C", "Beef, eggs, lentils, tofu, spinach, beans, or fortified cereal with citrus, berries, or kiwi.", "Food24"),
                Point("Warm carbs are useful", "Oats, rice, potatoes, soup, toast, or noodles calm the “nothing sounds good” problem.", "FoodGrains24"),
                Point("Salt and fluids", "If you feel flat or headachy, add electrolytes, broth, or salty food.", "DrinkCoffee24"),
                Point("Protein every meal", "Keep muscle repair boring and reliable: yogurt, eggs, fish, chicken, tofu, cottage cheese.", "FoodEgg24")
            ],
            [
                Point("Low impact first", "Walks, easy cycling, mobility, stretching, light machines, or technique practice.", "Run24"),
                Point("Strength if symptoms allow", "Use familiar lifts, fewer sets, and leave more reps in reserve.", "Dumbbell24"),
                Point("Core gently", "Swap hard bracing for breathing drills, dead bugs, or easy carries if cramps are present.", "PersonHeart24")
            ],
            [
                Point("Do not punish the scale", "Water retention and digestion changes can make weight noisy. Don't rewrite your whole plan from one bloated morning.", "AlertUrgent24"),
                Point("Watch unusual symptoms", "Severe pain, fainting, heavy bleeding, or sudden changes deserve a clinician, not a tougher workout.", "ShieldError24")
            ]),

        CyclePhase.Follicular => new CyclePhaseDetailContent(
            "Follicular",
            "Usually days 6-13",
            "Momentum comes back",
            "This is the week where “maybe I can” starts sounding believable again. Use it.",
            "Estrogen generally rises here, and many people feel better coordination, mood, and training tolerance.",
            "Fuel the climb. If training volume rises but food stays tiny, energy usually crashes later.",
            "Progressive overload belongs here: more load, one more set, sharper technique, new skills.",
            "Add challenge, not chaos. A little more each session beats a random heroic day.",
            [
                Point("Learning sticks better", "Higher energy and better coordination make this a strong window for practicing lifts or movements you want to own.", "BrainCircuit24"),
                Point("Work capacity often improves", "You may tolerate more volume or intensity. Track it so the plan can use it instead of guessing.", "ArrowTrending24"),
                Point("It sets up the power window", "Good follicular training makes ovulatory work feel earned, not accidental.", "Target24")
            ],
            [
                Point("Carbs around training", "Rice, oats, fruit, potatoes, pasta, or bread near harder sessions. This is not the week to fear fuel.", "Food24"),
                Point("Protein stays boring", "Keep the usual target. Muscle growth loves consistency more than dramatic food rules.", "FoodEgg24"),
                Point("Color for recovery", "Berries, greens, tomatoes, herbs, cruciferous veg, or citrus. Hard training creates recovery demand.", "FoodApple24"),
                Point("Do not coast under calories", "Feeling good can hide under-eating for a few days. The bill usually arrives in luteal.", "AlertUrgent24")
            ],
            [
                Point("Push strength", "Add weight, reps, or sets on main lifts while form is still clean.", "Dumbbell24"),
                Point("Practice new work", "New movements, technique blocks, plyo basics, or intervals fit well here.", "SparkleCircle24"),
                Point("Use honest progression", "If last week was rough, progress from your actual baseline, not the fantasy version of you.", "CheckmarkCircle24")
            ],
            [
                Point("Do not double everything", "Energy rising is not permission to add heavy legs, sprints, and a new class all at once.", "AlertUrgent24"),
                Point("Sleep still decides", "If sleep is bad, keep the progression smaller. Follicular is helpful, not magical.", "WeatherMoon24")
            ]),

        CyclePhase.Ovulatory => new CyclePhaseDetailContent(
            "Ovulatory",
            "Usually days 14-16",
            "The power window",
            "Short window, big output. This is where strength can feel unusually available.",
            "Around ovulation, many people feel peak drive, power, and confidence. The goal is clean aggression, not sloppy maxes.",
            "Match the output: carbs, electrolytes, protein, and enough total food to support heavy work.",
            "Use the window for heavy compounds, crisp speed work, or benchmark sets, with a real warm-up.",
            "Go heavy, not reckless. The best PR is the one you can recover from.",
            [
                Point("Performance can peak", "Strength, speed, and confidence may line up. Logging helps you know if this is true for you.", "Trophy24"),
                Point("Joint care matters", "Some people feel more joint looseness here. A better warm-up is not optional fluff.", "PersonHeart24"),
                Point("It is a measuring week", "Use it to test progress, not to prove your worth.", "Ruler24")
            ],
            [
                Point("Carbs before and after", "Fruit, rice, oats, potatoes, pasta, sports drink, or toast all work for heavy sessions.", "Food24"),
                Point("Electrolytes if you sweat", "Add sodium, potassium-rich foods, and fluids so performance doesn't get fake-fragile.", "DrinkCoffee24"),
                Point("Protein after hard work", "Get a real protein dose within the normal day. No drama, just don't skip it.", "FoodEgg24"),
                Point("Low appetite is not a plan", "Use easier foods: smoothies, yogurt bowls, wraps, rice bowls, protein shakes.", "FoodApple24")
            ],
            [
                Point("Heavy compounds", "Squat, hinge, press, pull, hip thrust, or weighted carries if your technique is ready.", "Dumbbell24"),
                Point("Power and speed", "Short sprints, jumps, med-ball throws, or low-volume explosive work.", "Flash24"),
                Point("Benchmark, then leave", "Hit the top set, log it, stop chasing extra attempts “just to see.”", "CheckmarkCircle24")
            ],
            [
                Point("No sloppy maxes", "If reps get twisty, grindy, or weird, the set is done.", "AlertUrgent24"),
                Point("Warm up longer than your ego wants", "Joints, ankles, hips, shoulders, and core bracing all get a vote here.", "ShieldError24")
            ]),

        CyclePhase.Luteal => new CyclePhaseDetailContent(
            "Luteal",
            "Usually days 17-28",
            "The steady builder",
            "You are not suddenly bad at fitness. The same output may simply cost more now.",
            "Progesterone rises, body temperature can shift, cravings may get louder, and recovery needs more respect.",
            "Plan food before cravings start yelling: steady meals, fiber, protein, magnesium, potassium.",
            "Keep strength in the plan, but trim the sharp edges: fewer all-out sets, better tempo, more recovery.",
            "Hold the line. This phase rewards consistency, not punishment.",
            [
                Point("Recovery gets more expensive", "Hard sessions may feel heavier even when the weight did not change.", "BatteryWarning24"),
                Point("Hunger is information", "A bigger appetite here is common. Pre-planned food beats late-night chaos.", "Food24"),
                Point("It protects the next cycle", "A good luteal plan prevents the menstrual phase from feeling like a full system crash.", "ShieldCheckmark24")
            ],
            [
                Point("Protein plus fiber", "Bowls, soups, chili, oats, berries, beans, veg. Protein keeps meals useful; fiber helps cravings.", "FoodEgg24"),
                Point("Do not delete carbs", "Carbs can help training, mood, and sleep. Choose steady sources before cravings choose for you.", "Food24"),
                Point("Magnesium and potassium", "Pumpkin seeds, dark chocolate, potatoes, bananas, beans, yogurt, spinach, avocado.", "FoodApple24"),
                Point("Prep the snack", "Have a planned sweet/salty option ready before hunger wins.", "Cookies24")
            ],
            [
                Point("Maintain strength", "Keep main lifts, but reduce volume if recovery feels slower. Quality reps over fatigue.", "Dumbbell24"),
                Point("Tempo and control", "Controlled eccentrics, machines, accessories, and moderate conditioning.", "Timer24"),
                Point("Late luteal deload", "If PMS, sleep, or soreness spikes, pull back before the crash. That is strategy, not quitting.", "ArrowTrendingDown24")
            ],
            [
                Point("Do not punish bloating", "Bloating is not a moral failure and not a reason to slash food.", "AlertUrgent24"),
                Point("Watch mood and sleep patterns", "If this phase consistently feels brutal, track it and bring it to a professional.", "ShieldError24")
            ]),

        _ => BuildDetail(CyclePhase.Follicular)
    };

    private static CyclePhaseDetailPoint Point(string title, string text, string iconGlyph) =>
        new() { Title = title, Text = text, IconGlyph = iconGlyph };

    private void ApplySignalModel(CyclePhase phase)
    {
        var signal = phase switch
        {
            CyclePhase.Menstrual => (Training: 0.35, Nutrition: 0.85, Recovery: 0.95, TrainingText: "Gentle", NutritionText: "Repair", RecoveryText: "High"),
            CyclePhase.Follicular => (Training: 0.78, Nutrition: 0.72, Recovery: 0.55, TrainingText: "Build", NutritionText: "Fuel", RecoveryText: "Steady"),
            CyclePhase.Ovulatory => (Training: 0.95, Nutrition: 0.82, Recovery: 0.70, TrainingText: "Power", NutritionText: "Support", RecoveryText: "Respect"),
            CyclePhase.Luteal => (Training: 0.58, Nutrition: 0.90, Recovery: 0.84, TrainingText: "Control", NutritionText: "Stabilize", RecoveryText: "Protect"),
            _ => (Training: 0.7, Nutrition: 0.7, Recovery: 0.5, TrainingText: "Build", NutritionText: "Balanced", RecoveryText: "Steady")
        };

        TrainingSignal = signal.Training;
        NutritionSignal = signal.Nutrition;
        RecoverySignal = signal.Recovery;
        TrainingSignalText = signal.TrainingText;
        NutritionSignalText = signal.NutritionText;
        RecoverySignalText = signal.RecoveryText;
    }

    private static string GetPhaseIconGlyph(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => "WeatherSnowflake24",
        CyclePhase.Follicular => "LeafThree24",
        CyclePhase.Ovulatory => "Flash24",
        CyclePhase.Luteal => "WeatherMoon24",
        _ => "LeafThree24"
    };

    private sealed record CyclePhaseDetailContent(
        string PhaseName,
        string PhaseWindow,
        string Headline,
        string Hook,
        string WhyLead,
        string NutritionLead,
        string WorkoutLead,
        string QuickRule,
        IReadOnlyList<CyclePhaseDetailPoint> WhyPoints,
        IReadOnlyList<CyclePhaseDetailPoint> NutritionPoints,
        IReadOnlyList<CyclePhaseDetailPoint> WorkoutPoints,
        IReadOnlyList<CyclePhaseDetailPoint> WatchPoints);
}
