using Raylib_cs;
using System;

namespace Cherris;

public sealed class DisplayServer
{
    private static DisplayServer? _instance;
    public static DisplayServer Instance => _instance ??= new();

    public Vector2 OriginalWindowSize = Vector2.Zero;
    public Vector2 PreviousWindowSize = Vector2.Zero;

    public static Vector2 WindowSize
    {
        get => new(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

        set
        {
            Raylib.SetWindowSize((int)value.X, (int)value.Y);
        }
    }

    public static Vector2 MonitorSize
    {
        get
        {
            int currentMonitor = Raylib.GetCurrentMonitor();
            int width = Raylib.GetMonitorWidth(currentMonitor);
            int height = Raylib.GetMonitorHeight(currentMonitor);

            return new(width, height);
        }
    }

    public static bool Fullscreen => Raylib.IsWindowFullscreen();

    // Events

    public event Action<Vector2>? WindowSizeChanged;

    // Public

    public DisplayServer()
    {
        PreviousWindowSize = WindowSize;
    }

    public void Process()
    {
        Vector2 currentWindowSize = WindowSize;

        if (currentWindowSize != PreviousWindowSize)
        {
            WindowSizeChanged?.Invoke(currentWindowSize);
            PreviousWindowSize = currentWindowSize;
        }
    }

    public static void ToggleFullscreen()
    {
        Raylib.ToggleFullscreen();
    }
}