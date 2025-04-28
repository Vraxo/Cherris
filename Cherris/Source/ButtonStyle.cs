// ButtonStyle.cs
using Vortice.Mathematics;
using Vortice.DirectWrite; // For Font related enums eventually

namespace Cherris;

public class ButtonStyle : BoxStyle
{
    // DirectWrite Font properties will be managed elsewhere (e.g., IDWriteTextFormat)
    // public float FontSpacing { get; set; } = 0; // Handled by TextFormat
    // public float FontSize { get; set; } = 16; // Handled by TextFormat
    public Font? Font { get; set; } = null; // Represented by TextFormat
    public Color4 FontColor { get; set; } = DefaultTheme.Text;

    // Properties needed for creating IDWriteTextFormat
    public string FontName { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 16.0f;
    public FontWeight FontWeight { get; set; } = FontWeight.Normal;
    public FontStyle FontStyle { get; set; } = FontStyle.Normal;
    public FontStretch FontStretch { get; set; } = FontStretch.Normal;
}