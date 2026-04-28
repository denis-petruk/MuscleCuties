using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Data;

public class AppDatabase : DbContext
{
    private readonly IDbPathProvider? _pathProvider;

    public AppDatabase(DbContextOptions<AppDatabase> options) : base(options) { }
    

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        if (_pathProvider != null)
            optionsBuilder.UseSqlite($"Filename={_pathProvider.GetDatabasePath()}");
    }
}
