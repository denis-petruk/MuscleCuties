namespace MuscleCuties.App.Controls.Shared;

public class AnimatedModalView : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(AnimatedModalView),
        false,
        propertyChanged: OnIsOpenChanged);

    private int _transitionVersion;

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

        if ((bool)newValue)
            modal.Open();
        else
            modal.Close();
    }

    private void Open()
    {
        _transitionVersion++;
        PrepareForOpen();
        IsVisible = true;
        InputTransparent = false;
        ModalTransition.PlayShow(this);
    }

    private void Close()
    {
        var transitionVersion = ++_transitionVersion;
        if (!IsVisible)
            return;

        InputTransparent = true;
        ModalTransition.PlayHide(this, () =>
        {
            if (transitionVersion != _transitionVersion || IsOpen)
                return;

            IsVisible = false;
        });
    }
}
