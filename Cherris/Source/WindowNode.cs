using System;
using System.Numerics;
using System.Collections.Generic;

namespace Cherris;

public class WindowNode : Node2D
{

    protected SecondaryWindow? secondaryWindow;
    private string windowTitle = "Cherris Window";
    private int windowWidth = 640;
    private int windowHeight = 480;

    protected bool isQueuedForFree = false;

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

    public override void Process()
    {

        base.Process();


        if (isQueuedForFree)
        {
            FreeInternal();
        }
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

            secondaryWindow = new SecondaryWindow(Title, this.Width, this.Height, this);

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


    public void QueueFree()
    {

        isQueuedForFree = true;
    }



    protected virtual void FreeInternal()
    {
        Log.Info($"Freeing WindowNode '{Name}' and its associated window.");
        secondaryWindow?.Close();

        secondaryWindow = null;
        base.Free();
    }


    public override void Free()
    {


        if (!isQueuedForFree)
        {
            Log.Warning($"Direct call to Free() on WindowNode '{Name}' detected. Use QueueFree() instead.");
            QueueFree();
        }
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


        var childrenToRender = new List<Node>(node.Children);
        foreach (Node child in childrenToRender)
        {
            RenderNodeRecursive(child, context);
        }
    }


    public SecondaryWindow? GetWindowHandle() => secondaryWindow;


    public Vector2 LocalMousePosition => secondaryWindow?.GetLocalMousePosition() ?? Input.MousePosition;

}