using Raylib_cs;

namespace Cherris;

public class Texture
{
    public Vector2 Size { get; private set; } = Vector2.Zero;
    private Texture2D raylibTexture;

    public Texture(string filePath)
    {
        string pngPath =
            Path.GetExtension(filePath).ToLower() == ".png" ?
            filePath :
            GetPngPath(filePath);

        raylibTexture = Raylib.LoadTexture(pngPath);
        Size = new(raylibTexture.Width, raylibTexture.Height);

        if (pngPath != filePath)
        {
            File.Delete(pngPath);
        }
    }

    public Texture()
    {
    }

    public static implicit operator Texture2D(Texture texture) => texture.raylibTexture;

    private static string GetPngPath(string imagePath)
    {
        if (!Directory.Exists("Res/Cherris/Temporary"))
        {
            Directory.CreateDirectory("Res/Temporary");
        }

        string pngPath = $"Res/Cherris/Temporary/{Path.GetFileNameWithoutExtension(imagePath)}.png";

        if (!File.Exists(pngPath))
        {
            //using var image = SixLabors.ImageSharp.Image.Load(imagePath);
            //image.SaveAsPng(pngPath);
        }

        return pngPath;
    }
}