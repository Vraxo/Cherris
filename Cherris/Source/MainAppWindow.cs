using System;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using System.Numerics;

namespace Cherris;

public class MainAppWindow : Direct2DAppWindow
{
    public event Action? Closed;
    private bool _firstDrawLogged = false;

    public MainAppWindow(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        Input.SetupDefaultActions();
    }

    protected override void DrawUIContent(DrawingContext context)
    {
        if (!_firstDrawLogged)
        {
            Log.Info($"MainAppWindow.DrawUIContent called for '{Title}'. Rendering SceneTree.");
            _firstDrawLogged = true;
        }

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

    protected override IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        int xPos = NativeMethods.GET_X_LPARAM(lParam);
        int yPos = NativeMethods.GET_Y_LPARAM(lParam);
        Vector2 mousePos = new Vector2(xPos, yPos);

        switch (msg)
        {
            case NativeMethods.WM_MOUSEMOVE:
                Input.UpdateMousePosition(mousePos);
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Left, true);
                return IntPtr.Zero;
            case NativeMethods.WM_LBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Left, false);
                return IntPtr.Zero;

            case NativeMethods.WM_RBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Right, true);
                return IntPtr.Zero;
            case NativeMethods.WM_RBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Right, false);
                return IntPtr.Zero;

            case NativeMethods.WM_MBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Middle, true);
                return IntPtr.Zero;
            case NativeMethods.WM_MBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Middle, false);
                return IntPtr.Zero;

            case NativeMethods.WM_XBUTTONDOWN:
                int xButton1 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton1 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, true);
                if (xButton1 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, true);
                return IntPtr.Zero;
            case NativeMethods.WM_XBUTTONUP:
                int xButton2 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton2 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, false);
                if (xButton2 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, false);
                return IntPtr.Zero;

            case NativeMethods.WM_MOUSEWHEEL:
                short wheelDelta = NativeMethods.GET_WHEEL_DELTA_WPARAM(wParam);
                Input.UpdateMouseWheel((float)wheelDelta / NativeMethods.WHEEL_DELTA);
                return IntPtr.Zero;

            case NativeMethods.WM_KEYDOWN:
            case NativeMethods.WM_SYSKEYDOWN:
                int vkCodeDown = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeDown))
                {
                    Input.UpdateKey((KeyCode)vkCodeDown, true);
                }
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_KEYUP:
            case NativeMethods.WM_SYSKEYUP:
                int vkCodeUp = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeUp))
                {
                    Input.UpdateKey((KeyCode)vkCodeUp, false);
                }
                return IntPtr.Zero;
        }

        return base.HandleMessage(hWnd, msg, wParam, lParam);
    }
}