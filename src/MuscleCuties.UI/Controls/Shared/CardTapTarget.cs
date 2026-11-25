namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// A transparent button placed over a card-style surface. The card keeps its visual treatment, while
/// VoiceOver gets a real button with a label and hint instead of an unnamed generic container.
/// </summary>
public sealed class CardTapTarget : Button
{
    /// <summary>Makes a card's whole surface pressable while leaving its inner controls able to take their own taps.</summary>
    public CardTapTarget()
    {
        Text = string.Empty;
        BackgroundColor = Colors.Transparent;
        BorderWidth = 0;
        Padding = 0;
        MinimumHeightRequest = 0;
        MinimumWidthRequest = 0;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
    }
}
