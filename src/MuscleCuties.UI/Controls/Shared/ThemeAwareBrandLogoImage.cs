using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Shared;

public sealed class ThemeAwareBrandLogoImage : ThemeAwareImage
{
    public ThemeAwareBrandLogoImage()
    {
        LightSource = "musclecuties_logo_light.png";
        DarkSource = "musclecuties_logo_dark.png";
        Aspect = Aspect.AspectFit;
        BackgroundColor = AppThemeResources.GetRequired<Color>("Transparent");
    }
}
