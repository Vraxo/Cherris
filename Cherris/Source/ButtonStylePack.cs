namespace Cherris;

public sealed class ButtonStylePack
{
    public ButtonStyle Current { get; set; } = new();
    public ButtonStyle Normal { get; set; } = new();
    public ButtonStyle Hover { get; set; } = new();
    public ButtonStyle Pressed { get; set; } = new();
    public ButtonStyle Disabled { get; set; } = new();
    public ButtonStyle Focused { get; set; } = new();

    private IEnumerable<ButtonStyle> AllStyles => [Current, Normal, Hover, Pressed, Disabled, Focused];

    public ButtonStylePack()
    {
        Hover.FillColor = DefaultTheme.HoverFill;

        Pressed.FillColor = DefaultTheme.Accent;

        Disabled.FillColor = DefaultTheme.DisabledFill;
        Disabled.BorderColor = DefaultTheme.DisabledBorder;
        Disabled.FontColor = DefaultTheme.DisabledText;

        Focused.BorderColor = DefaultTheme.FocusBorder;
        Focused.BorderLength = 1;
    }

    public float FontSize
    {
        get;
        set
        {
            field = value;
            SetAll(s => s.FontSize = value);
        }
    } = 0;

    public Font Font
    {
        set => SetAll(s => s.Font = value);
    }

    public Color FontColor
    {
        set => SetAll(s => s.FontColor = value);
    }

    public float Roundness
    {
        set => SetAll(s => s.Roundness = value);
    }

    public float BorderLength
    {
        set => SetAll(s => s.BorderLength = value);
    }

    public Color FillColor
    {
        set => SetAll(s => s.FillColor = value);
    }

    public Color BorderColor
    {
        set => SetAll(s => s.BorderColor = value);
    }

    public float BorderLengthTop
    {
        set => SetAll(s => s.BorderLengthTop = value);
    }

    public float BorderLengthBottom
    {
        set => SetAll(s => s.BorderLengthBottom = value);
    }

    private void SetAll(Action<ButtonStyle> setter)
    {
        foreach (ButtonStyle style in AllStyles)
        {
            setter(style);
        }
    }
}