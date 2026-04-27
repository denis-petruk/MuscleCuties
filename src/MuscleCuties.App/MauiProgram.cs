using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
// TODO: Add using statements after Core and App namespaces are set up

namespace MuscleCuties.App;

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
                fonts.AddFont("Nunito-Variable.ttf", "NunitoRegular");
                fonts.AddFont("Fraunces-Variable.ttf", "FrauncesDisplay");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
