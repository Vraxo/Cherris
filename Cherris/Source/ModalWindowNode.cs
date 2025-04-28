using System;
using System.Numerics;
using System.Reflection;

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


            this.secondaryWindow = modalWindow;


            if (!modalWindow.TryCreateWindow())
            {
                Log.Error($"ModalWindowNode '{Name}' failed to create its modal window.");
                modalWindow = null;
                this.secondaryWindow = null;
                return;
            }

            if (!modalWindow.InitializeWindowAndGraphics())
            {
                Log.Error($"ModalWindowNode '{Name}' failed to initialize modal window graphics.");
                modalWindow.Dispose();
                modalWindow = null;
                this.secondaryWindow = null;
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
            this.secondaryWindow = null;
        }
    }




    protected override void FreeInternal()
    {
        Log.Info($"Freeing ModalWindowNode '{Name}' and its associated modal window.");

        modalWindow?.Close();
        modalWindow = null;


        this.secondaryWindow = null;


        base.FreeInternal();
    }



    public override void Process()
    {


        if (this.isQueuedForFree)
        {
            this.FreeInternal();
        }
        else
        {


            base.Process();
        }
    }


}