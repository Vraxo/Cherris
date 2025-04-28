using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Numerics;

namespace Cherris;

public abstract class Win32Window : IDisposable
{
    private readonly string _windowClassName;
    private readonly string _windowTitle;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hInstance = IntPtr.Zero;
    private NativeMethods.WndProc _wndProcDelegate;
    private bool _isDisposed = false;
    private static readonly HashSet<string> RegisteredClassNames = new HashSet<string>();
    private GCHandle _gcHandle;

    public IntPtr Handle => _hwnd;
    public string Title => _windowTitle;
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    public bool IsOpen { get; private set; } = false;


    protected Win32Window(string title, int width, int height, string className = null)
    {
        _windowTitle = title ?? "Win32 Window";
        _initialWidth = width > 0 ? width : 800;
        _initialHeight = height > 0 ? height : 600;
        Width = _initialWidth;
        Height = _initialHeight;
        _windowClassName = className ?? ("Win32Window_" + Guid.NewGuid().ToString("N"));
        _wndProcDelegate = WindowProcedure;
        Input.SetupDefaultActions();
    }

    public bool TryCreateWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            Log.Warning("Window handle already exists. Creation skipped.");
            return true;
        }

        _hInstance = NativeMethods.GetModuleHandle(null);
        if (_hInstance == IntPtr.Zero)
        {
            _hInstance = Process.GetCurrentProcess().Handle;
        }

        lock (RegisteredClassNames)
        {
            if (!RegisteredClassNames.Contains(_windowClassName))
            {
                var wndClass = new NativeMethods.WNDCLASSEX
                {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                    style = NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW | NativeMethods.CS_OWNDC,
                    lpfnWndProc = _wndProcDelegate,
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = _hInstance,
                    hIcon = NativeMethods.LoadIcon(IntPtr.Zero, (IntPtr)NativeMethods.IDI_APPLICATION),
                    hCursor = NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW),
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null,
                    lpszClassName = _windowClassName,
                    hIconSm = NativeMethods.LoadIcon(IntPtr.Zero, (IntPtr)NativeMethods.IDI_APPLICATION)
                };

                if (NativeMethods.RegisterClassEx(ref wndClass) == 0)
                {
                    Log.Error($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                RegisteredClassNames.Add(_windowClassName);
                Log.Info($"Class '{_windowClassName}' registered.");
            }
        }

        _gcHandle = GCHandle.Alloc(this);
        _hwnd = NativeMethods.CreateWindowEx(
            0,
            _windowClassName,
            _windowTitle,
            NativeMethods.WS_OVERLAPPEDWINDOW,
            NativeMethods.CW_USEDEFAULT, NativeMethods.CW_USEDEFAULT,
            _initialWidth, _initialHeight,
            IntPtr.Zero, IntPtr.Zero, _hInstance, GCHandle.ToIntPtr(_gcHandle));

        if (_hwnd == IntPtr.Zero)
        {
            Log.Error($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
            if (_gcHandle.IsAllocated) _gcHandle.Free();
            return false;
        }

        Log.Info($"Window created with HWND: {_hwnd}");
        IsOpen = true;
        return true;
    }

    public void ShowWindow()
    {
        if (_hwnd != IntPtr.Zero && IsOpen)
        {
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNORMAL);
            NativeMethods.UpdateWindow(_hwnd);
        }
        else
        {
            Log.Warning("Cannot show window: Handle is zero or window is not open.");
        }
    }

    public bool InitializeWindowAndGraphics()
    {
        if (_hwnd == IntPtr.Zero || !IsOpen)
        {
            Log.Error("Cannot initialize: Window handle is invalid or window is closed.");
            return false;
        }


        return Initialize();
    }

    public void ProcessMessages()
    {
        while (NativeMethods.PeekMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
        {
            if (msg.message == NativeMethods.WM_QUIT)
            {
                Log.Info("WM_QUIT received, signaling window close.");
                IsOpen = false;
                break;
            }

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Win32Window? window = null;

        if (msg == NativeMethods.WM_NCCREATE)
        {
            try
            {
                var cs = Marshal.PtrToStructure<NativeMethods.CREATESTRUCT>(lParam);
                var handle = GCHandle.FromIntPtr(cs.lpCreateParams);
                window = handle.Target as Win32Window;
                if (window != null)
                {
                    NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, GCHandle.ToIntPtr(handle));
                    Log.Info($"WM_NCCREATE: Associated instance with HWND {hWnd}");
                }
                else
                {
                    Log.Warning($"WM_NCCREATE: Failed to get window instance from GCHandle for HWND {hWnd}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error during WM_NCCREATE: {ex}");
            }
        }
        else
        {
            IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
            if (ptr != IntPtr.Zero)
            {
                try
                {
                    var handle = GCHandle.FromIntPtr(ptr);
                    if (handle.IsAllocated && handle.Target != null)
                    {
                        window = handle.Target as Win32Window;
                    }
                    else
                    {
                        Log.Warning($"WindowProcedure: GCHandle {ptr} is not allocated or target is null for HWND {hWnd}, Msg={msg}");
                    }
                }
                catch (InvalidOperationException)
                {
                    Log.Warning($"WindowProcedure: Invalid GCHandle {ptr} retrieved for HWND {hWnd}, Msg={msg}. Resetting GWLP_USERDATA.");

                    NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error retrieving GCHandle: {ex}");
                }
            }
        }

        if (window != null)
        {
            try
            {
                return window.HandleMessage(hWnd, msg, wParam, lParam);
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling message {msg} for HWND {hWnd}: {ex}");
            }
        }
        else if (msg != NativeMethods.WM_NCCREATE && msg != NativeMethods.WM_NCDESTROY)
        {
            Log.Info($"WindowProcedure: No window instance found for HWND {hWnd}, Msg={msg}. Using DefWindowProc.", true);
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    protected virtual IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        int xPos = NativeMethods.GET_X_LPARAM(lParam);
        int yPos = NativeMethods.GET_Y_LPARAM(lParam);
        Vector2 mousePos = new Vector2(xPos, yPos);

        switch (msg)
        {
            case NativeMethods.WM_PAINT:
                RenderFrame();
                NativeMethods.ValidateRect(hWnd, IntPtr.Zero);
                return IntPtr.Zero;

            case NativeMethods.WM_SIZE:
                Width = NativeMethods.LOWORD(lParam);
                Height = NativeMethods.HIWORD(lParam);
                OnSize(Width, Height);
                return IntPtr.Zero;

            case NativeMethods.WM_MOUSEMOVE:
                Input.UpdateMousePosition(mousePos);
                OnMouseMove(xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Left, true);
                OnMouseDown(MouseButton.Left, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Left, false);
                OnMouseUp(MouseButton.Left, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_RBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Right, true);
                OnMouseDown(MouseButton.Right, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_RBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Right, false);
                OnMouseUp(MouseButton.Right, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_MBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Middle, true);
                OnMouseDown(MouseButton.Middle, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_MBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Middle, false);
                OnMouseUp(MouseButton.Middle, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_XBUTTONDOWN:
                int xButton1 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton1 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, true);
                if (xButton1 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, true);
                OnMouseDown(xButton1 == NativeMethods.XBUTTON1 ? MouseButton.XButton1 : MouseButton.XButton2, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_XBUTTONUP:
                int xButton2 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton2 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, false);
                if (xButton2 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, false);
                OnMouseUp(xButton2 == NativeMethods.XBUTTON1 ? MouseButton.XButton1 : MouseButton.XButton2, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_MOUSEWHEEL:
                short wheelDelta = NativeMethods.GET_WHEEL_DELTA_WPARAM(wParam);
                Input.UpdateMouseWheel((float)wheelDelta / NativeMethods.WHEEL_DELTA);
                OnMouseWheel(wheelDelta);
                return IntPtr.Zero;

            case NativeMethods.WM_KEYDOWN:
            case NativeMethods.WM_SYSKEYDOWN:
                int vkCodeDown = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeDown))
                {
                    Input.UpdateKey((KeyCode)vkCodeDown, true);
                }
                OnKeyDown(vkCodeDown);

                if (vkCodeDown == NativeMethods.VK_ESCAPE && !IsKeyDownHandled(vkCodeDown))
                {
                    Close();
                }
                return IntPtr.Zero;

            case NativeMethods.WM_KEYUP:
            case NativeMethods.WM_SYSKEYUP:
                int vkCodeUp = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeUp))
                {
                    Input.UpdateKey((KeyCode)vkCodeUp, false);
                }
                OnKeyUp(vkCodeUp);
                return IntPtr.Zero;

            case NativeMethods.WM_CLOSE:
                if (OnClose())
                {
                    NativeMethods.DestroyWindow(hWnd);
                }
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                Log.Info($"WM_DESTROY for {hWnd}.");
                OnDestroy();

                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;

            case NativeMethods.WM_NCDESTROY:
                Log.Info($"WM_NCDESTROY: Releasing GCHandle for {hWnd}.");
                IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
                if (ptr != IntPtr.Zero)
                {
                    try
                    {
                        var handle = GCHandle.FromIntPtr(ptr);
                        if (handle.IsAllocated)
                        {
                            handle.Free();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error freeing GCHandle on NCDESTROY: {ex.Message}");
                    }

                    NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
                }

                if (_gcHandle.IsAllocated && GCHandle.ToIntPtr(_gcHandle) == ptr)
                {
                    _gcHandle = default;
                }
                _hwnd = IntPtr.Zero;
                IsOpen = false;
                return IntPtr.Zero;

            default:
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    public void Close()
    {
        if (_hwnd != IntPtr.Zero && IsOpen)
        {
            Log.Info($"Programmatically closing window {_hwnd}.");
            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void Invalidate()
    {
        if (_hwnd != IntPtr.Zero && IsOpen)
        {
            NativeMethods.InvalidateRect(_hwnd, IntPtr.Zero, false);
        }
    }


    protected abstract bool Initialize();
    public abstract void RenderFrame();
    protected virtual void OnSize(int width, int height) { }
    protected virtual void OnMouseDown(MouseButton button, int x, int y) { }
    protected virtual void OnMouseUp(MouseButton button, int x, int y) { }
    protected virtual void OnMouseMove(int x, int y) { }
    protected virtual void OnKeyDown(int virtualKeyCode) { }
    protected virtual void OnKeyUp(int virtualKeyCode) { }
    protected virtual void OnMouseWheel(short delta) { }
    protected virtual bool IsKeyDownHandled(int virtualKeyCode) { return false; }
    protected virtual bool OnClose() { return true; }
    protected virtual void OnDestroy() { }
    protected abstract void Cleanup();


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                Log.Info("Disposing Win32Window (managed)...");
                Cleanup();
            }

            Log.Info("Disposing Win32Window (unmanaged)...");
            if (_hwnd != IntPtr.Zero)
            {
                Log.Info($"Requesting destroy for window {_hwnd} during Dispose...");


                NativeMethods.DestroyWindow(_hwnd);

            }
            else
            {

                if (_gcHandle.IsAllocated)
                {
                    Log.Warning("Freeing potentially dangling GCHandle during Dispose (window handle was already zero)...");
                    try { _gcHandle.Free(); } catch (Exception ex) { Log.Error($"Error freeing GCHandle: {ex.Message}"); }
                }
            }

            _isDisposed = true;
            IsOpen = false;
            Log.Info("Win32Window disposed.");
        }
    }

    ~Win32Window()
    {
        Log.Warning("Win32Window Finalizer called!");
        Dispose(false);
    }
}


public enum MouseButton { Left, Right, Middle, XButton1, XButton2 }