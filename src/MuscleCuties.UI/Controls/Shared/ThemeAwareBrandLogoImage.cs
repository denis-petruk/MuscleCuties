namespace MuscleCuties.UI.Controls.Shared;

public sealed class ThemeAwareBrandLogoImage : ThemeAwareImage
{
    public ThemeAwareBrandLogoImage()
    {
        LightSource = "musclecuties_logo_light.png";
        DarkSource = "musclecuties_logo_dark.png";
        Aspect = Aspect.AspectFit;
        BackgroundColor = Colors.Transparent;
    }
}
