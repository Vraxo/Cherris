using Raylib_cs;

namespace Cherris;

public class Font
{
    public string Name = "";
    public int Size = 0;

    private Raylib_cs.Font raylibFont;

    public Vector2 Dimensions
    {
        get
        {
            return Raylib.MeasureTextEx(raylibFont, " ", Size, 0);
        }
    }

    public Font(string filePath, int size)
    {
        Size = size;
        Name = Path.GetFileNameWithoutExtension(filePath);

        int[] codepoints = new int[255 - 32 + 1];
        for (int i = 0; i < codepoints.Length; i++)
        {
            codepoints[i] = 32 + i;
        }

        raylibFont = Raylib.LoadFontEx(filePath, size, codepoints, codepoints.Length);
        Raylib.SetTextureFilter(raylibFont.Texture, TextureFilter.Bilinear);
    }

    public static implicit operator Raylib_cs.Font(Font textFont) => textFont.raylibFont;

    public static Vector2 MeasureText(Font font, string text, int size, float spacing)
    {
        Vector2 measurements = Raylib.MeasureTextEx(
            font,
            text,
            size,
            spacing);

        return measurements;
    }
}