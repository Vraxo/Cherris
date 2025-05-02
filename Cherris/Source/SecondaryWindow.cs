using System;
using System.Collections.Generic;
using System.Numerics;

namespace Cherris;

public class SecondaryWindow : Direct2DAppWindow
{
    private readonly WindowNode ownerNode;
    private Vector2 currentMousePosition = Vector2.Zero;

    public SecondaryWindow(string title, int width, int height, WindowNode owner)
        : base(title, width, height)
    {
        ownerNode = owner ?? throw new ArgumentNullException(nameof(owner));
        ApplicationCore.Instance.RegisterSecondaryWindow(this);
    }

    protected override void DrawUIContent(DrawingContext context)
    {
        ownerNode?.RenderChildren(context);
    }

    protected override bool OnClose()
    {
        Log.Info($"SecondaryWindow '{Title}' OnClose called.");
        ownerNode?.QueueFree();
        return base.OnClose();
    }

    protected override void Cleanup()
    {
        Log.Info($"SecondaryWindow '{Title}' Cleanup starting.");
        base.Cleanup();
        Log.Info($"SecondaryWindow '{Title}' Cleanup finished.");
    }

    protected override IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        int xPos = NativeMethods.GET_X_LPARAM(lParam);
        int yPos = NativeMethods.GET_Y_LPARAM(lParam);
        Vector2 mousePos = new Vector2(xPos, yPos);

        currentMousePosition = mousePos;

        switch (msg)
        {
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

    public Vector2 GetLocalMousePosition() => currentMousePosition;
}