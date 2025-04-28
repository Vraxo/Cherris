using System;
using System.Numerics;

namespace Cherris;

public class WindowNode : Node
{
    private SecondaryWindow? secondaryWindow;
    private string windowTitle = "Cherris Window";
    private int windowWidth = 640;
    private int windowHeight = 480;

    public string Title
    {
        get => windowTitle;
        set
        {
            if (windowTitle == value) return;
            windowTitle = value;

        }
    }

    public int Width
    {
        get => windowWidth;
        set
        {
            if (windowWidth == value) return;
            windowWidth = value;

        }
    }

    public int Height
    {
        get => windowHeight;
        set
        {
            if (windowHeight == value) return;
            windowHeight = value;

        }
    }

    public override void Make()
    {
        base.Make();
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        if (secondaryWindow is not null)
        {
            Log.Warning($"WindowNode '{Name}' already has an associated window. Skipping creation.");
            return;
        }

        try
        {
            secondaryWindow = new SecondaryWindow(Title, Width, Height, this);

            if (!secondaryWindow.TryCreateWindow())
            {
                Log.Error($"WindowNode '{Name}' failed to create its window.");
                secondaryWindow = null;
                return;
            }

            if (!secondaryWindow.InitializeWindowAndGraphics())
            {
                Log.Error($"WindowNode '{Name}' failed to initialize window graphics.");
                secondaryWindow.Dispose();
                secondaryWindow = null;
                return;
            }

            secondaryWindow.ShowWindow();
            Log.Info($"WindowNode '{Name}' successfully created and initialized its window.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error during WindowNode '{Name}' initialization: {ex.Message}");
            secondaryWindow?.Dispose();
            secondaryWindow = null;
        }
    }

    public override void Free()
    {
        Log.Info($"Freeing WindowNode '{Name}' and its associated window.");
        secondaryWindow?.Close();
        secondaryWindow?.Dispose();
        secondaryWindow = null;
        base.Free();
    }

    internal void RenderChildren(DrawingContext context)
    {

        foreach (Node child in Children)
        {
            RenderNodeRecursive(child, context);
        }
    }

    private static void RenderNodeRecursive(Node node, DrawingContext context)
    {

        if (node is WindowNode)
        {

            return;
        }

        if (node is VisualItem { Visible: true } visualItem)
        {
            visualItem.Draw(context);
        }


        foreach (Node child in node.Children)
        {
            RenderNodeRecursive(child, context);
        }
    }

    public bool IsLocalKeyDown(KeyCode key)
    {

        return secondaryWindow?.IsKeyDown(key) ?? false;
    }

    public bool IsLocalMouseButtonDown(MouseButtonCode button)
    {

        return secondaryWindow?.IsMouseButtonDown(button) ?? false;
    }

    public Vector2 LocalMousePosition
    {

        get => secondaryWindow?.GetMousePosition() ?? Vector2.Zero;
    }
}