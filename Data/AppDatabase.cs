using Microsoft.EntityFrameworkCore;
using MuscleCuties.Models;

namespace MuscleCuties.Data;

public class AppDatabase : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserBaselineProfile> UserBaselineProfiles { get; set; }
    public DbSet<QuizQuestion> QuizQuestions { get; set; }
    public DbSet<QuizAnswer> QuizAnswers { get; set; }
    public DbSet<CycleLog> CycleLogs { get; set; }
    public DbSet<SymptomLog> SymptomLogs { get; set; }
    public DbSet<WorkoutPlan> WorkoutPlans { get; set; }
    public DbSet<WorkoutDay> WorkoutDays { get; set; }
    public DbSet<Exercise> Exercises { get; set; }
    public DbSet<WorkoutExercise> WorkoutExercises { get; set; }
    public DbSet<FoodItem> FoodItems { get; set; }
    public DbSet<FoodLog> FoodLogs { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<UserQuizResponse> UserQuizResponses { get; set; }
    public DbSet<RecipeIngredient> RecipeIngredients { get; set; }
    public DbSet<DailyRecommendation> DailyRecommendations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "musclecuties.db");
        optionsBuilder.UseSqlite($"Filename={dbPath}");
    }

    public async Task InitializeAsync()
    {
        await Database.EnsureCreatedAsync();
    }
}