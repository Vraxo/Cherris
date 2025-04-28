using System;

namespace Cherris;

public class ModalSecondaryWindow : SecondaryWindow
{


    public ModalSecondaryWindow(string title, int width, int height, WindowNode ownerNode, IntPtr ownerHandle)
        : base(title, width, height, ownerNode)
    {

    }


    public override bool TryCreateWindow(IntPtr ownerHwndOverride = default, uint? styleOverride = null)
    {

        uint defaultModalStyle = NativeMethods.WS_POPUP | NativeMethods.WS_CAPTION | NativeMethods.WS_SYSMENU |
                                 NativeMethods.WS_THICKFRAME;


        var ownerHwnd = ApplicationCore.Instance.GetMainWindowHandle();

        return base.TryCreateWindow(ownerHwnd, styleOverride ?? defaultModalStyle);
    }

    public override void ShowWindow()
    {


        base.ShowWindow();
        ApplicationCore.Instance.RegisterModal(this);
    }

    protected override void OnDestroy()
    {


        Log.Info($"Modal '{Title}' OnDestroy. Unregistering from modal stack.");
        ApplicationCore.Instance.UnregisterModal(this);
        base.OnDestroy();
    }


    protected override bool OnClose()
    {
        Log.Info($"ModalSecondaryWindow '{Title}' OnClose called.");


        return base.OnClose();
    }


    protected override IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        bool logInput = false;

        switch (msg)
        {
            case NativeMethods.WM_LBUTTONDOWN:
                Log.Info($"Modal '{Title}' received WM_LBUTTONDOWN", logInput);
                break;
            case NativeMethods.WM_LBUTTONUP:
                Log.Info($"Modal '{Title}' received WM_LBUTTONUP", logInput);
                break;
            case NativeMethods.WM_MOUSEMOVE:

                break;

            case NativeMethods.WM_ACTIVATE:
                Log.Info($"Modal '{Title}' received WM_ACTIVATE (wParam: {wParam})", logInput);
                break;
            case NativeMethods.WM_NCACTIVATE:
                Log.Info($"Modal '{Title}' received WM_NCACTIVATE (wParam: {wParam})", logInput);
                break;
        }


        return base.HandleMessage(hWnd, msg, wParam, lParam);
    }
}