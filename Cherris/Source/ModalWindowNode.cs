using System;
using System.Numerics; // Added for Vector2
using System.Reflection; // Keep for now if needed, but should remove

namespace Cherris;

public class ModalWindowNode : WindowNode
{
    private ModalSecondaryWindow? modalWindow;


    public override void Make()
    {

        InitializeModalWindow();
    }


    private void InitializeModalWindow()
    {
        if (modalWindow is not null)
        {
            Log.Warning($"ModalWindowNode '{Name}' already has an associated window. Skipping creation.");
            return;
        }

        var ownerHandle = ApplicationCore.Instance.GetMainWindowHandle();
        if (ownerHandle == IntPtr.Zero)
        {
            Log.Error($"ModalWindowNode '{Name}' could not get the main window handle. Cannot create modal window.");
            return;
        }

        try
        {

            modalWindow = new ModalSecondaryWindow(Title, Width, Height, this, ownerHandle);


            // Assign to the protected base field
            this.secondaryWindow = modalWindow;


            if (!modalWindow.TryCreateWindow())
            {
                Log.Error($"ModalWindowNode '{Name}' failed to create its modal window.");
                modalWindow = null;
                this.secondaryWindow = null; // Clear base reference on failure
                return;
            }

            if (!modalWindow.InitializeWindowAndGraphics())
            {
                Log.Error($"ModalWindowNode '{Name}' failed to initialize modal window graphics.");
                modalWindow.Dispose();
                modalWindow = null;
                this.secondaryWindow = null; // Clear base reference on failure
                return;
            }

            modalWindow.ShowWindow();
            Log.Info($"ModalWindowNode '{Name}' successfully created and initialized its modal window.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error during ModalWindowNode '{Name}' initialization: {ex.Message}");
            modalWindow?.Dispose();
            modalWindow = null;
            this.secondaryWindow = null; // Clear base reference on failure
        }
    }


    // No need for AssignWindowReference helper anymore


    // Override FreeInternal to handle modalWindow and call base
    protected override void FreeInternal()
    {
        Log.Info($"Freeing ModalWindowNode '{Name}' and its associated modal window.");
        // Close the specific modal window reference first
        modalWindow?.Close();
        modalWindow = null;

        // Also null out the base reference it was using
        this.secondaryWindow = null;

        // Now call the base implementation which handles Node.Free()
        base.FreeInternal();
    }


    // Process needs to check the protected base flag
    public override void Process()
    {

        // Access the protected flag directly
        if (this.isQueuedForFree)
        {
            this.FreeInternal(); // Call our override
        }
        else
        {
            // Input update uses the base reference (secondaryWindow),
            // which we set to modalWindow in InitializeModalWindow.
            this.secondaryWindow?.UpdateLocalInput();

            // Call Node/Node2D Process logic
            base.Process();
        }
    }

    // QueueFree and Free can just use the base implementations now
}