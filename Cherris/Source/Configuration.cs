namespace Cherris;

public class Configuration
{
    public int Width { get; set; } = 0;
    public int Height { get; set; } = 0;
    public int MinWidth { get; set; } = 0;
    public int MinHeight { get; set; } = 0;
    public int MaxWidth { get; set; } = 0;
    public int MaxHeight { get; set; } = 0;
    public string Title { get; set; } = "Cherris";
    public bool ResizableWindow { get; set; } = true;
    public bool AntiAliasing { get; set; } = true;
    public string MainScenePath { get; set; } = "";
    public string Backend { get; set; } = "Raylib";
}