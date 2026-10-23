using System.Diagnostics.CodeAnalysis;

[assembly:
    SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Scope = "namespace",
        Target = "~N:MuscleCuties.UI.Controls.Shared", Justification = "Shared UI controls namespace.")]

namespace MuscleCuties.UI.Controls.Shared;

public class AnimatedModalView : ContentView
{
    public static readonly BindableProperty IsOpenProperty =
        BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(AnimatedModalView), false,
            propertyChanged: OnIsOpenChanged);

    public AnimatedModalView()
    {
        IsVisible = false;
        InputTransparent = true;
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected virtual void PrepareForOpen()
    {
    }

    private static void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not AnimatedModalView modal)
            return;

        var open = (bool)newValue;
        if (open)
        {
            modal.PrepareForOpen();
            modal.IsVisible = true;
            modal.InputTransparent = false;
            ModalTransition.PlayShow(modal);
        }
        else
        {
            modal.InputTransparent = true;
            ModalTransition.PlayHide(modal, () =>
            {
                if (!modal.IsOpen)
                    modal.IsVisible = false;
            });
        }
    }
}
