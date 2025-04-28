using System;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Cherris;

public class MainAppWindow : Direct2DAppWindow
{
    public event Action? Closed;

    public MainAppWindow(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {

    }

    protected override void DrawUIContent(DrawingContext context)
    {
        SceneTree.Instance.RenderScene(context);
    }

    protected override bool OnClose()
    {
        Log.Info("MainAppWindow OnClose called.");
        Closed?.Invoke();
        return base.OnClose();
    }

    protected override void Cleanup()
    {
        Log.Info("MainAppWindow Cleanup starting.");

        base.Cleanup();
        Log.Info("MainAppWindow Cleanup finished.");
    }
}