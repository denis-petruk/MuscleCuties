using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MuscleCuties.Data;
using MuscleCuties.Repositories;
using MuscleCuties.Services;

namespace MuscleCuties;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddDbContext<AppDatabase>();

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ICycleRepository, CycleRepository>();
        builder.Services.AddScoped<ISymptomRepository, SymptomRepository>();
        builder.Services.AddScoped<IQuizRepository, QuizRepository>();
        builder.Services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        builder.Services.AddScoped<INutritionRepository, NutritionRepository>();
        builder.Services.AddScoped<IDailyRecommendationRepository, DailyRecommendationRepository>();

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<ICycleService, CycleService>();
        builder.Services.AddScoped<INutritionService, NutritionService>();
        builder.Services.AddScoped<IQuizService, QuizService>();

        builder.Services.AddTransient<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDatabase>();
        db.InitializeAsync().GetAwaiter().GetResult();

        return app;
    }
}