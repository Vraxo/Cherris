using System;
using System.Numerics;

namespace Cherris;

public class WindowNode : Node
{
    private SecondaryWindow? secondaryWindow;
    private string windowTitle = "Cherris Window";
    private int windowWidth = 640;
    private int windowHeight = 480;
    private bool isQueuedForFree = false;

    public string Title
    {
        get => windowTitle;
        set
        {
            if (windowTitle == value) return;
            windowTitle = value;
            // TODO: Add method to update existing window title if needed
        }
    }

    public int Width
    {
        get => windowWidth;
        set
        {
            if (windowWidth == value) return;
            windowWidth = value;
            // TODO: Add method to resize existing window if needed
        }
    }

    public int Height
    {
        get => windowHeight;
        set
        {
            if (windowHeight == value) return;
            windowHeight = value;
            // TODO: Add method to resize existing window if needed
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
        secondaryWindow?.UpdateLocalInput(); // Update previous frame state

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
                secondaryWindow.Dispose(); // Clean up partially created window
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


    private void FreeInternal()
    {
        Log.Info($"Freeing WindowNode '{Name}' and its associated window.");
        secondaryWindow?.Close(); // Request OS window close
                                  // Actual disposal happens via WM_NCDESTROY -> Unregister -> Win32Window.Dispose
        secondaryWindow = null; // Remove reference
        base.Free(); // Free the node itself from the tree
    }


    public override void Free()
    {
        // This override prevents direct freeing. Use QueueFree() instead.
        // The actual cleanup happens in FreeInternal() when processed.
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


        var childrenToRender = new List<Node>(node.Children); // Copy list for safe iteration
        foreach (Node child in childrenToRender)
        {
            RenderNodeRecursive(child, context);
        }
    }


    public SecondaryWindow? GetWindowHandle() => secondaryWindow;

    // Convenience methods to access local input state
    public bool IsLocalKeyDown(KeyCode key) => secondaryWindow?.IsKeyDown(key) ?? false;
    public bool IsLocalMouseButtonDown(MouseButtonCode button) => secondaryWindow?.IsMouseButtonDown(button) ?? false;
    public Vector2 LocalMousePosition => secondaryWindow?.GetMousePosition() ?? Vector2.Zero;
    public bool IsLocalKeyPressed(KeyCode key) => secondaryWindow?.IsKeyPressed(key) ?? false;
    public bool IsLocalKeyReleased(KeyCode key) => secondaryWindow?.IsKeyReleased(key) ?? false;
    public bool IsLocalMouseButtonPressed(MouseButtonCode button) => secondaryWindow?.IsMouseButtonPressed(button) ?? false;
    public bool IsLocalMouseButtonReleased(MouseButtonCode button) => secondaryWindow?.IsMouseButtonReleased(button) ?? false;
    public float LocalMouseWheelMovement => secondaryWindow?.GetMouseWheelMovement() ?? 0f;
}