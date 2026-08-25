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
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _whyPoints = new();
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _nutritionPoints = new();
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _workoutPoints = new();
    [ObservableProperty] private ObservableCollection<CyclePhaseDetailPoint> _watchPoints = new();

    public RelayCommand BackCommand { get; }

    public CyclePhaseDetailViewModel(Action navigateBack)
    {
        _navigateBack = navigateBack;
        BackCommand = new RelayCommand(_navigateBack);
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
        WhyPoints = new ObservableCollection<CyclePhaseDetailPoint>(detail.WhyPoints);
        NutritionPoints = new ObservableCollection<CyclePhaseDetailPoint>(detail.NutritionPoints);
        WorkoutPoints = new ObservableCollection<CyclePhaseDetailPoint>(detail.WorkoutPoints);
        WatchPoints = new ObservableCollection<CyclePhaseDetailPoint>(detail.WatchPoints);
    }

    private static CyclePhaseDetailContent BuildDetail(CyclePhase phase) => phase switch
    {
        CyclePhase.Menstrual => new CyclePhaseDetailContent(
            "Menstrual",
            "Usually days 1-5",
            "The reset window",
            "This is not a failed training week. It is the part where your body asks for a cleaner plan.",
            "Hormones are low, inflammation can be louder, and cramps can steal output before you even start. Tracking this phase helps you stop confusing lower capacity with lower discipline.",
            "Replace restriction with repair: iron, fluids, steady carbs, and protein that does not require heroic cooking.",
            "Keep the habit alive, lower the noise. Movement should make the day easier, not turn cramps into a boss fight.",
            "If pain is high, train the routine: walk, mobilize, eat, sleep. That still counts.",
            [
                Point("Energy has a real reason to dip", "Lower estrogen and progesterone can make heavy work feel expensive. Plan for that instead of blaming yourself."),
                Point("Bleeding changes nutrition math", "Iron and fluid losses are small for some people and very real for others. Logs help you notice your own pattern."),
                Point("Recovery gives you better later weeks", "A slightly easier few days often protects follicular and ovulatory training quality.")
            ],
            [
                Point("Iron plus vitamin C", "Think beef, eggs, lentils, tofu, spinach, beans, or fortified cereal with citrus, berries, bell pepper, or kiwi."),
                Point("Warm carbs are useful", "Oats, rice, potatoes, soup, toast, or noodles can calm the “nothing sounds good” problem while still fueling you."),
                Point("Salt and fluids", "If you feel flat or headachy, water alone may not be enough. Add electrolytes, broth, or salty food."),
                Point("Protein every meal", "Keep muscle repair boring and reliable: Greek yogurt, eggs, fish, chicken, tofu, protein oats, cottage cheese.")
            ],
            [
                Point("Low impact first", "Walks, easy cycling, mobility, stretching, light machines, or technique practice are perfect here."),
                Point("Strength if symptoms allow", "Use familiar lifts, fewer sets, and leave more reps in reserve. No need to audition for a PR."),
                Point("Core gently", "If cramps are present, swap hard bracing for breathing drills, dead bugs, or carries only if they feel good.")
            ],
            [
                Point("Do not punish the scale", "Water retention and digestion changes can make weight noisy. Do not rewrite your whole plan from one bloated morning."),
                Point("Watch unusual symptoms", "Severe pain, fainting, heavy bleeding, or sudden changes deserve a clinician, not a tougher workout.")
            ]),

        CyclePhase.Follicular => new CyclePhaseDetailContent(
            "Follicular",
            "Usually days 6-13",
            "Momentum comes back",
            "This is the week where “maybe I can” starts sounding believable again. Use it.",
            "Estrogen generally rises here, and many people feel better coordination, mood, and training tolerance. This is a great time to build skill and add workload carefully.",
            "Fuel the climb. If training volume rises but food stays tiny, energy usually crashes later.",
            "Progressive overload belongs here: more load, one more set, sharper technique, new skills, or harder conditioning.",
            "Add challenge, not chaos. A little more each session beats a random heroic day.",
            [
                Point("Learning sticks better", "Higher energy and better coordination make this a strong window for practicing lifts or movements you want to own."),
                Point("Work capacity often improves", "You may tolerate more volume or intensity. Track it so the plan can use it instead of guessing."),
                Point("It sets up the power window", "Good follicular training makes ovulatory work feel earned, not accidental.")
            ],
            [
                Point("Carbs around training", "Put rice, oats, fruit, potatoes, pasta, or bread near harder sessions. This is not the week to fear fuel."),
                Point("Protein stays boring", "Keep the usual target. Muscle growth loves consistency more than dramatic food rules."),
                Point("Color for recovery", "Add berries, greens, tomatoes, herbs, cruciferous veg, or citrus. Not because it is cute: because hard training creates recovery demand."),
                Point("Do not coast under calories", "Feeling good can hide under-eating for a few days. The bill usually arrives in luteal.")
            ],
            [
                Point("Push strength", "Add weight, reps, or sets on main lifts while form is still clean."),
                Point("Practice new work", "This is a good place for new movements, technique blocks, plyo basics, or intervals."),
                Point("Use honest progression", "If last week was rough, progress from your actual baseline, not from the fantasy version of you.")
            ],
            [
                Point("Do not double everything", "Energy rising is not permission to add heavy legs, sprints, and a new class all at once."),
                Point("Sleep still decides", "If sleep is bad, keep the progression smaller. Follicular is helpful, not magical.")
            ]),

        CyclePhase.Ovulatory => new CyclePhaseDetailContent(
            "Ovulatory",
            "Usually days 14-16",
            "The power window",
            "Short window, big output. This is where strength can feel unusually available.",
            "Around ovulation, many people feel peak drive, power, and confidence. That makes this phase useful, but it also tempts sloppy maxes. The goal is clean aggression.",
            "Match the output: carbs, electrolytes, protein, and enough total food to support heavy work.",
            "Use the window for heavy compounds, crisp speed work, or benchmark sets, with a warm-up that respects your joints.",
            "Go heavy, not reckless. The best PR is the one you can recover from.",
            [
                Point("Performance can peak", "Strength, speed, and confidence may line up. Logging helps you know if this is true for you."),
                Point("Joint care matters", "Some people feel a little more joint looseness here. A better warm-up is not optional fluff."),
                Point("It is a measuring week", "Use it to test progress, not to prove your worth.")
            ],
            [
                Point("Carbs before and after", "Heavy sessions want glycogen. Fruit, rice, oats, potatoes, pasta, sports drink, or toast all work."),
                Point("Electrolytes if you sweat", "High output plus poor hydration makes performance fake-fragile. Add sodium, potassium-rich foods, and fluids."),
                Point("Protein after hard work", "Get a real protein dose within the normal day. No drama, just do not skip it."),
                Point("Low appetite is not a plan", "If appetite dips, use easier foods: smoothies, yogurt bowls, wraps, rice bowls, protein shakes.")
            ],
            [
                Point("Heavy compounds", "Squat, hinge, press, pull, hip thrust, or weighted carries can live here if your technique is ready."),
                Point("Power and speed", "Short sprints, jumps, med-ball throws, or low-volume explosive work fit well when warm-up feels sharp."),
                Point("Benchmark, then leave", "Hit the top set, log it, stop chasing ten more “just to see” attempts.")
            ],
            [
                Point("No sloppy maxes", "If reps get twisty, grindy, or weird, the set is done."),
                Point("Warm up longer than your ego wants", "Joints, ankles, hips, shoulders, and core bracing all get a vote here.")
            ]),

        CyclePhase.Luteal => new CyclePhaseDetailContent(
            "Luteal",
            "Usually days 17-28",
            "The steady builder",
            "You are not suddenly bad at fitness. The same output may simply cost more now.",
            "Progesterone rises, body temperature can shift, cravings may get louder, and recovery can need more respect. This phase is where smart plans stop fighting biology.",
            "Plan food before cravings start yelling: steady meals, fiber, protein, magnesium, potassium, and enough carbs to stay sane.",
            "Keep strength in the plan, but trim the sharp edges: fewer all-out sets, better tempo, more recovery space.",
            "Hold the line. This phase rewards consistency, not punishment.",
            [
                Point("Recovery gets more expensive", "Hard sessions may feel heavier even when the weight did not change. That is useful planning data."),
                Point("Hunger is information", "A bigger appetite here is common. Pre-planned food beats late-night chaos."),
                Point("It protects the next cycle", "A good luteal plan prevents the menstrual phase from feeling like a full system crash.")
            ],
            [
                Point("Protein plus fiber", "Protein keeps meals useful; fiber helps cravings and digestion. Think bowls, soups, chili, oats, berries, beans, veg."),
                Point("Do not delete carbs", "Carbs can help training, mood, and sleep. Choose steady sources before cravings choose for you."),
                Point("Magnesium and potassium", "Pumpkin seeds, dark chocolate, potatoes, bananas, beans, yogurt, spinach, and avocado can help the basics."),
                Point("Prep the snack", "Have a planned sweet/salty option. Better a controlled choice than fighting hunger until it wins.")
            ],
            [
                Point("Maintain strength", "Keep main lifts, but reduce volume if recovery feels slower. Quality reps over dramatic fatigue."),
                Point("Tempo and control", "Controlled eccentrics, machines, accessories, and moderate conditioning are your friends here."),
                Point("Late luteal deload", "If PMS, sleep, or soreness spikes, pull back before the crash. That is strategy, not quitting.")
            ],
            [
                Point("Do not punish bloating", "Bloating is not a moral failure and not a reason to slash food."),
                Point("Watch mood and sleep patterns", "If this phase consistently feels brutal, it is worth tracking and bringing to a professional.")
            ]),

        _ => BuildDetail(CyclePhase.Follicular)
    };

    private static CyclePhaseDetailPoint Point(string title, string text) =>
        new() { Title = title, Text = text };

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
