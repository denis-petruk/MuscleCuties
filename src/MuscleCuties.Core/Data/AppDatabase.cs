using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.Data;

public class AppDatabase : DbContext
{
    public AppDatabase(DbContextOptions<AppDatabase> options) : base(options) { }

    // User domain
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserProfileSnapshot> UserProfileSnapshots => Set<UserProfileSnapshot>();

    // Quiz domain
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    public DbSet<UserQuizResponse> UserQuizResponses => Set<UserQuizResponse>();

    // Cycle domain
    public DbSet<CycleLog> CycleLogs => Set<CycleLog>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();

    // Nutrition domain
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<FoodItemVersion> FoodItemVersions => Set<FoodItemVersion>();
    public DbSet<FoodSyncLog> FoodSyncLogs => Set<FoodSyncLog>();
    public DbSet<MealTemplate> MealTemplates => Set<MealTemplate>();
    public DbSet<MealTemplateEntry> MealTemplateEntries => Set<MealTemplateEntry>();
    public DbSet<LoggedMeal> LoggedMeals => Set<LoggedMeal>();
    public DbSet<LoggedMealEntry> LoggedMealEntries => Set<LoggedMealEntry>();

    // Workout domain
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutDay> WorkoutDays => Set<WorkoutDay>();
    public DbSet<WorkoutDayExercise> WorkoutDayExercises => Set<WorkoutDayExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();

    // Recommendation domain
    public DbSet<RecommendationSet> RecommendationSets => Set<RecommendationSet>();
    public DbSet<NutritionRecommendation> NutritionRecommendations => Set<NutritionRecommendation>();
    public DbSet<WorkoutRecommendation> WorkoutRecommendations => Set<WorkoutRecommendation>();
    public DbSet<WellnessRecommendation> WellnessRecommendations => Set<WellnessRecommendation>();
}