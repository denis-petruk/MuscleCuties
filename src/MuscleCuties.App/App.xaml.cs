using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Services;

namespace MuscleCuties.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_services.GetRequiredService<AppShell>());
    }

    protected override async void OnStart()
    {
        base.OnStart();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDatabase>();
        #if DEBUG
        db.Database.EnsureDeleted();
        var tokenStorage = scope.ServiceProvider.GetRequiredService<ITokenStorage>();
        tokenStorage.RemoveAll();
        #endif

        db.Database.EnsureCreated();

        if (!await db.AreExercisesSeededAsync())
            await db.SeedExercisesAsync();

        if (!await db.AreQuestionsSeededAsync())
            await db.SeedQuizAsync();

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var isLoggedIn = await authService.IsLoggedInAsync();
        if (isLoggedIn)
            await Shell.Current.GoToAsync("//DashboardPage");
    }
}
