using MuscleCuties.Data;

namespace MuscleCuties;

public partial class App : Application
{
    public App(AppDatabase database)
    {
        InitializeComponent();
        Task.Run(async () => await database.InitializeAsync());
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}