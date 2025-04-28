using System;
using System.Numerics;
using System.Collections.Generic; // Added for List

namespace Cherris;

public class WindowNode : Node2D
{
    // Make secondaryWindow protected so ModalWindowNode can assign to it
    protected SecondaryWindow? secondaryWindow;
    private string windowTitle = "Cherris Window";
    private int windowWidth = 640;
    private int windowHeight = 480;
    // Make isQueuedForFree protected so ModalWindowNode can check it if needed
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
        secondaryWindow?.UpdateLocalInput();

        if (isQueuedForFree)
        {
            FreeInternal();
        }
    }

    // Make InitializeWindow virtual if ModalWindowNode needs to override
    // or change its behavior significantly, otherwise protected might be fine.
    // For now, ModalWindowNode bypasses it in Make(), so it doesn't strictly matter.
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


    // Change access modifier to protected
    protected virtual void FreeInternal()
    {
        Log.Info($"Freeing WindowNode '{Name}' and its associated window.");
        secondaryWindow?.Close();

        secondaryWindow = null;
        base.Free(); // Call Node.Free() to remove from parent etc.
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


    public bool IsLocalKeyDown(KeyCode key) => secondaryWindow?.IsKeyDown(key) ?? false;
    public bool IsLocalMouseButtonDown(MouseButtonCode button) => secondaryWindow?.IsMouseButtonDown(button) ?? false;
    public Vector2 LocalMousePosition => secondaryWindow?.GetMousePosition() ?? Vector2.Zero;
    public bool IsLocalKeyPressed(KeyCode key) => secondaryWindow?.IsKeyPressed(key) ?? false;
    public bool IsLocalKeyReleased(KeyCode key) => secondaryWindow?.IsKeyReleased(key) ?? false;
    public bool IsLocalMouseButtonPressed(MouseButtonCode button) => secondaryWindow?.IsMouseButtonPressed(button) ?? false;
    public bool IsLocalMouseButtonReleased(MouseButtonCode button) => secondaryWindow?.IsMouseButtonReleased(button) ?? false;
    public float LocalMouseWheelMovement => secondaryWindow?.GetMouseWheelMovement() ?? 0f;
}