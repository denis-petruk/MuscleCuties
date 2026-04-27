using Microsoft.EntityFrameworkCore;
using MuscleCuties.Models;
using MuscleCuties.Models.Enums;

namespace MuscleCuties.Data;

public class AppDatabase : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserBaselineProfile> UserBaselineProfiles => Set<UserBaselineProfile>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    public DbSet<CycleLog> CycleLogs => Set<CycleLog>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutDay> WorkoutDays => Set<WorkoutDay>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<FoodLog> FoodLogs => Set<FoodLog>();
    public DbSet<UserQuizResponse> UserQuizResponses => Set<UserQuizResponse>();
    public DbSet<DailyRecommendation> DailyRecommendations => Set<DailyRecommendation>();

    private static bool _debugResetDone;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "musclecuties.db");
        optionsBuilder.UseSqlite($"Filename={dbPath}");
    }

    public async Task InitializeAsync(bool resetInDebug = false)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "musclecuties.db");

#if DEBUG
        if (resetInDebug && !_debugResetDone)
        {
            _debugResetDone = true;

            if (File.Exists(dbPath))
                File.Delete(dbPath);

            SecureStorage.RemoveAll();
        }
#endif
        if (File.Exists(dbPath))
            File.Delete(dbPath);

        SecureStorage.RemoveAll();
        await Database.EnsureCreatedAsync();
        await SeedQuizQuestionsAsync();
    }
    private async Task SeedQuizQuestionsAsync()
    {
        if (await QuizQuestions.AnyAsync())
            return;
        // MappedValue conventions:
        //   Goal            → (int)UserGoal enum
        //   ExperienceLevel → 0=Beginner, 1=Intermediate, 2=Advanced
        //   WorkoutDays     → actual int (3, 4, 5, 6)
        //   DietaryPref     → (int)DietaryTag enum
        //   Pain/Energy/Mood per phase → 1–5 scale
        var questions = new List<QuizQuestion>
        {
            // ── General ──────────────────────────────────────────────────────────
            new()
            {
                OrderIndex   = 1,
                QuestionType = QuizQuestionType.Goal,
                Question     = "What is your primary fitness goal?",
                Answers      =
                [
                    new() { Text = "Lose body fat",          OrderIndex = 1, MappedValue = (int)UserGoal.FatLoss },
                    new() { Text = "Tone and build muscle",  OrderIndex = 2, MappedValue = (int)UserGoal.MuscleTone },
                    new() { Text = "Build strength",         OrderIndex = 3, MappedValue = (int)UserGoal.Strength },
                    new() { Text = "Maintain overall health",OrderIndex = 4, MappedValue = (int)UserGoal.MaintainHealth }
                ]
            },
            new()
            {
                OrderIndex   = 2,
                QuestionType = QuizQuestionType.ExperienceLevel,
                Question     = "What is your current training experience?",
                Answers      =
                [
                    new() { Text = "Beginner (under 6 months)",      OrderIndex = 1, MappedValue = 0 },
                    new() { Text = "Intermediate (6 months – 2 yrs)",OrderIndex = 2, MappedValue = 1 },
                    new() { Text = "Advanced (2+ years)",            OrderIndex = 3, MappedValue = 2 }
                ]
            },
            new()
            {
                OrderIndex   = 3,
                QuestionType = QuizQuestionType.WorkoutDaysPerWeek,
                Question     = "How many days per week can you train?",
                Answers      =
                [
                    new() { Text = "3 days", OrderIndex = 1, MappedValue = 3 },
                    new() { Text = "4 days", OrderIndex = 2, MappedValue = 4 },
                    new() { Text = "5 days", OrderIndex = 3, MappedValue = 5 },
                    new() { Text = "6 days", OrderIndex = 4, MappedValue = 6 }
                ]
            },
            new()
            {
                OrderIndex   = 4,
                QuestionType = QuizQuestionType.DietaryPreference,
                Question     = "Do you follow any dietary preference?",
                Answers      =
                [
                    new() { Text = "No restrictions",OrderIndex = 1, MappedValue = (int)DietaryTag.None },
                    new() { Text = "Vegetarian",     OrderIndex = 2, MappedValue = (int)DietaryTag.Vegetarian },
                    new() { Text = "Vegan",          OrderIndex = 3, MappedValue = (int)DietaryTag.Vegan },
                    new() { Text = "Gluten-free",    OrderIndex = 4, MappedValue = (int)DietaryTag.GlutenFree },
                    new() { Text = "Lactose-free",   OrderIndex = 5, MappedValue = (int)DietaryTag.LactoseFree }
                ]
            },
            // ── Menstrual phase ───────────────────────────────────────────────────
            new()
            {
                OrderIndex   = 5,
                QuestionType = QuizQuestionType.MenstrualPain,
                Question     = "During your period, how intense is your pain typically?",
                Answers      = PainAnswers()
            },
            new()
            {
                OrderIndex   = 6,
                QuestionType = QuizQuestionType.MenstrualEnergy,
                Question     = "During your period, how would you describe your energy level?",
                Answers      = EnergyAnswers()
            },
            // ── Follicular phase ──────────────────────────────────────────────────
            new()
            {
                OrderIndex   = 7,
                QuestionType = QuizQuestionType.FollicularPain,
                Question     = "After your period (follicular phase), do you experience pain or discomfort?",
                Answers      = PainAnswers()
            },
            new()
            {
                OrderIndex   = 8,
                QuestionType = QuizQuestionType.FollicularEnergy,
                Question     = "After your period (follicular phase), how is your energy level?",
                Answers      = EnergyAnswers()
            },
            // ── Ovulatory phase ───────────────────────────────────────────────────
            new()
            {
                OrderIndex   = 9,
                QuestionType = QuizQuestionType.OvulatoryPain,
                Question     = "Around ovulation, do you experience pain or discomfort?",
                Answers      = PainAnswers()
            },
            new()
            {
                OrderIndex   = 10,
                QuestionType = QuizQuestionType.OvulatoryEnergy,
                Question     = "Around ovulation, how is your energy level?",
                Answers      = EnergyAnswers()
            },
            // ── Luteal phase ──────────────────────────────────────────────────────
            new()
            {
                OrderIndex   = 11,
                QuestionType = QuizQuestionType.LutealPain,
                Question     = "In the week before your period (luteal phase), do you experience pain or PMS symptoms?",
                Answers      = PainAnswers()
            },
            new()
            {
                OrderIndex   = 12,
                QuestionType = QuizQuestionType.LutealEnergy,
                Question     = "In the week before your period (luteal phase), how is your energy level?",
                Answers      = EnergyAnswers()
            }
        };

        await QuizQuestions.AddRangeAsync(questions);
        await SaveChangesAsync();
    }

    // Pain: 1=None → 5=Severe
    private static List<QuizAnswer> PainAnswers() =>
    [
        new() { Text = "None",     OrderIndex = 1, MappedValue = 1 },
        new() { Text = "Mild",     OrderIndex = 2, MappedValue = 2 },
        new() { Text = "Moderate", OrderIndex = 3, MappedValue = 3 },
        new() { Text = "Strong",   OrderIndex = 4, MappedValue = 4 },
        new() { Text = "Severe",   OrderIndex = 5, MappedValue = 5 }
    ];

    // Energy: 1=Very low → 5=Very high
    private static List<QuizAnswer> EnergyAnswers() =>
    [
        new() { Text = "Very low",  OrderIndex = 1, MappedValue = 1 },
        new() { Text = "Low",       OrderIndex = 2, MappedValue = 2 },
        new() { Text = "Moderate",  OrderIndex = 3, MappedValue = 3 },
        new() { Text = "High",      OrderIndex = 4, MappedValue = 4 },
        new() { Text = "Very high", OrderIndex = 5, MappedValue = 5 }
    ];
}