using System; // Required for Action, ArgumentNullException etc.
using System.Collections.Generic; // Required for HashSet, Dictionary etc.
using System.Numerics; // Required for Vector2

namespace Cherris;

public class SecondaryWindow : Direct2DAppWindow
{
    private readonly WindowNode ownerNode;
    private readonly HashSet<KeyCode> currentKeysDown = [];
    private readonly HashSet<MouseButtonCode> currentMouseButtonsDown = [];
    private Vector2 currentMousePosition = Vector2.Zero;
    private float currentMouseWheelDelta = 0f;
    private readonly HashSet<KeyCode> previousKeysDown = [];
    private readonly HashSet<MouseButtonCode> previousMouseButtonsDown = [];

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


    public void UpdateLocalInput()
    {
        previousMouseButtonsDown.Clear();
        foreach (var button in currentMouseButtonsDown)
        {
            previousMouseButtonsDown.Add(button);
        }

        previousKeysDown.Clear();
        foreach (var key in currentKeysDown)
        {
            previousKeysDown.Add(key);
        }
        currentMouseWheelDelta = 0f;
    }

    protected override IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        int xPos = NativeMethods.GET_X_LPARAM(lParam);
        int yPos = NativeMethods.GET_Y_LPARAM(lParam);
        Vector2 mousePos = new Vector2(xPos, yPos);

        switch (msg)
        {
            case NativeMethods.WM_MOUSEMOVE:
                currentMousePosition = mousePos;
                break;

            case NativeMethods.WM_LBUTTONDOWN:
                currentMouseButtonsDown.Add(MouseButtonCode.Left);
                break;
            case NativeMethods.WM_LBUTTONUP:
                currentMouseButtonsDown.Remove(MouseButtonCode.Left);
                break;

            case NativeMethods.WM_RBUTTONDOWN:
                currentMouseButtonsDown.Add(MouseButtonCode.Right);
                break;
            case NativeMethods.WM_RBUTTONUP:
                currentMouseButtonsDown.Remove(MouseButtonCode.Right);
                break;

            case NativeMethods.WM_MBUTTONDOWN:
                currentMouseButtonsDown.Add(MouseButtonCode.Middle);
                break;
            case NativeMethods.WM_MBUTTONUP:
                currentMouseButtonsDown.Remove(MouseButtonCode.Middle);
                break;

            case NativeMethods.WM_XBUTTONDOWN:
                int xButton1 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton1 == NativeMethods.XBUTTON1) currentMouseButtonsDown.Add(MouseButtonCode.Side);
                if (xButton1 == NativeMethods.XBUTTON2) currentMouseButtonsDown.Add(MouseButtonCode.Extra);
                break;
            case NativeMethods.WM_XBUTTONUP:
                int xButton2 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton2 == NativeMethods.XBUTTON1) currentMouseButtonsDown.Remove(MouseButtonCode.Side);
                if (xButton2 == NativeMethods.XBUTTON2) currentMouseButtonsDown.Remove(MouseButtonCode.Extra);
                break;

            case NativeMethods.WM_MOUSEWHEEL:
                short wheelDelta = NativeMethods.GET_WHEEL_DELTA_WPARAM(wParam);
                currentMouseWheelDelta = (float)wheelDelta / NativeMethods.WHEEL_DELTA;
                break;

            case NativeMethods.WM_KEYDOWN:
            case NativeMethods.WM_SYSKEYDOWN:
                int vkCodeDown = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeDown))
                {
                    currentKeysDown.Add((KeyCode)vkCodeDown);
                }
                break;

            case NativeMethods.WM_KEYUP:
            case NativeMethods.WM_SYSKEYUP:
                int vkCodeUp = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeUp))
                {
                    currentKeysDown.Remove((KeyCode)vkCodeUp);
                }
                break;
        }


        return base.HandleMessage(hWnd, msg, wParam, lParam);
    }


    public bool IsKeyDown(KeyCode key) => currentKeysDown.Contains(key);
    public bool IsMouseButtonDown(MouseButtonCode button) => currentMouseButtonsDown.Contains(button);
    public Vector2 GetMousePosition() => currentMousePosition;

    public bool IsKeyPressed(KeyCode key) => currentKeysDown.Contains(key) && !previousKeysDown.Contains(key);
    public bool IsKeyReleased(KeyCode key) => !currentKeysDown.Contains(key) && previousKeysDown.Contains(key);
    public bool IsMouseButtonPressed(MouseButtonCode button) => currentMouseButtonsDown.Contains(button) && !previousMouseButtonsDown.Contains(button);
    public bool IsMouseButtonReleased(MouseButtonCode button) => !currentMouseButtonsDown.Contains(button) && previousMouseButtonsDown.Contains(button);
    public float GetMouseWheelMovement() => currentMouseWheelDelta;

}