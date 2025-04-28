using System;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using System.Numerics; // Required for Vector2

namespace Cherris;

public class MainAppWindow : Direct2DAppWindow
{
    public event Action? Closed;

    public MainAppWindow(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        // Ensure global input actions are set up once
        Input.SetupDefaultActions();
    }

    protected override void DrawUIContent(DrawingContext context)
    {
        SceneTree.Instance.RenderScene(context);
    }

    protected override bool OnClose()
    {
        Log.Info("MainAppWindow OnClose called.");
        Closed?.Invoke();
        return base.OnClose(); // Use base implementation which returns true
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
                break;

            case NativeMethods.WM_LBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Left, true);
                break;
            case NativeMethods.WM_LBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Left, false);
                break;

            case NativeMethods.WM_RBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Right, true);
                break;
            case NativeMethods.WM_RBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Right, false);
                break;

            case NativeMethods.WM_MBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Middle, true);
                break;
            case NativeMethods.WM_MBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Middle, false);
                break;

            case NativeMethods.WM_XBUTTONDOWN:
                int xButton1 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton1 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, true);
                if (xButton1 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, true);
                break;
            case NativeMethods.WM_XBUTTONUP:
                int xButton2 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton2 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, false);
                if (xButton2 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, false);
                break;

            case NativeMethods.WM_MOUSEWHEEL:
                short wheelDelta = NativeMethods.GET_WHEEL_DELTA_WPARAM(wParam);
                Input.UpdateMouseWheel((float)wheelDelta / NativeMethods.WHEEL_DELTA);
                break;

            case NativeMethods.WM_KEYDOWN:
            case NativeMethods.WM_SYSKEYDOWN:
                int vkCodeDown = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeDown))
                {
                    Input.UpdateKey((KeyCode)vkCodeDown, true);
                }
                break;

            case NativeMethods.WM_KEYUP:
            case NativeMethods.WM_SYSKEYUP:
                int vkCodeUp = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeUp))
                {
                    Input.UpdateKey((KeyCode)vkCodeUp, false);
                }
                break;
        }


        return base.HandleMessage(hWnd, msg, wParam, lParam);
    }
}