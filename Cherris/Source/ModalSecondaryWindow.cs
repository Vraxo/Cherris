using System;

namespace Cherris;

public class ModalSecondaryWindow : SecondaryWindow
{
    private readonly IntPtr ownerHwnd;

    public ModalSecondaryWindow(string title, int width, int height, WindowNode ownerNode, IntPtr ownerHandle)
        : base(title, width, height, ownerNode)
    {
        ownerHwnd = ownerHandle;
    }


    public override bool TryCreateWindow(IntPtr ownerHwndOverride = default, uint? styleOverride = null)
    {

        uint defaultModalStyle = NativeMethods.WS_POPUP
                               | NativeMethods.WS_CAPTION
                               | NativeMethods.WS_SYSMENU
                               | NativeMethods.WS_VISIBLE
                               | NativeMethods.WS_THICKFRAME;


        return base.TryCreateWindow(ownerHwnd, styleOverride ?? defaultModalStyle);
    }

    public override void ShowWindow()
    {

        if (ownerHwnd != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(ownerHwnd, false);
        }
        base.ShowWindow();
    }

    protected override void OnDestroy()
    {

        if (ownerHwnd != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(ownerHwnd, true);
        }
        base.OnDestroy();
    }


    protected override bool OnClose()
    {
        Log.Info($"ModalSecondaryWindow '{Title}' OnClose called.");

        if (ownerHwnd != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(ownerHwnd, true);
        }


        return base.OnClose();
    }
}