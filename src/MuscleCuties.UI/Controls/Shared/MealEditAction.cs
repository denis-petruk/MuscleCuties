using Microsoft.Maui.Controls;

namespace MuscleCuties.UI.Controls.Shared;

/// <summary>
/// The one "edit this meal" action. The nutrition day page (a meal card) and the nutrition history
/// breakdown both call it, so the two cannot drift: type, colour, size, spacing, tap target, text
/// scale and automation identity all come from this control instead of from each call site.
/// Callers pass the command, its parameter, the localized text and visibility only.
/// </summary>
public sealed class MealEditAction : Button
{
    /// <summary>
    /// The single automation identity for this action. Automation taps the meal edit on either
    /// page by this id, so a wrapped or re-worded label can never break the flow.
    /// </summary>
    public const string AutomationIdentity = "MealEditAction";

    public MealEditAction()
    {
        AutomationId = AutomationIdentity;
        SetDynamicResource(StyleProperty, "ButtonSecondary");
        // The action sits at the trailing edge of its own row on both pages.
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.Center;
    }
}
