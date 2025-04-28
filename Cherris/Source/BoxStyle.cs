// MODIFIED: Styles/BoxStyle.cs
// Summary: Replaced BorderThickness with BorderLengthTop, BorderLengthRight, BorderLengthBottom, BorderLengthLeft. Added convenience setter BorderLength.
using Vortice.Mathematics;

namespace Cherris;

public class BoxStyle
{
    public float Roundness { get; set; } = 0.2f;
    public Color4 FillColor { get; set; } = DefaultTheme.NormalFill;
    public Color4 BorderColor { get; set; } = DefaultTheme.NormalBorder;

    public float BorderLengthTop { get; set; } = 1.0f;
    public float BorderLengthRight { get; set; } = 1.0f;
    public float BorderLengthBottom { get; set; } = 1.0f;
    public float BorderLengthLeft { get; set; } = 1.0f;

    public float BorderLength
    {
        set
        {
            BorderLengthTop = value;
            BorderLengthRight = value;
            BorderLengthBottom = value;
            BorderLengthLeft = value;
        }
    }
}