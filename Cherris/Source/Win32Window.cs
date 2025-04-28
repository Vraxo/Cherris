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
    private static readonly Dictionary<string, NativeMethods.WndProc> RegisteredClassProcedures = new();
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
    }

    public virtual bool TryCreateWindow(IntPtr ownerHwnd = default, uint? styleOverride = null)
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

        lock (RegisteredClassProcedures)
        {
            if (!RegisteredClassProcedures.ContainsKey(_windowClassName))
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
                RegisteredClassProcedures.Add(_windowClassName, _wndProcDelegate);
                Log.Info($"Class '{_windowClassName}' registered.");
            }
        }

        _gcHandle = GCHandle.Alloc(this);

        uint windowStyle = styleOverride ?? NativeMethods.WS_OVERLAPPEDWINDOW;

        _hwnd = NativeMethods.CreateWindowEx(
            0,
            _windowClassName,
            _windowTitle,
            windowStyle,
            NativeMethods.CW_USEDEFAULT, NativeMethods.CW_USEDEFAULT,
            _initialWidth, _initialHeight,
            ownerHwnd,
            IntPtr.Zero,
            _hInstance,
            GCHandle.ToIntPtr(_gcHandle));

        if (_hwnd == IntPtr.Zero)
        {
            Log.Error($"CreateWindowEx failed for '{_windowTitle}': {Marshal.GetLastWin32Error()}");
            if (_gcHandle.IsAllocated) _gcHandle.Free();
            return false;
        }

        Log.Info($"Window '{_windowTitle}' created with HWND: {_hwnd}");
        IsOpen = true;
        return true;
    }

    public virtual void ShowWindow()
    {
        if (_hwnd != IntPtr.Zero && IsOpen)
        {
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNORMAL);
            NativeMethods.UpdateWindow(_hwnd);
        }
        else
        {
            Log.Warning($"Cannot show window '{Title}': Handle is zero or window is not open.");
        }
    }

    public bool InitializeWindowAndGraphics()
    {
        if (_hwnd == IntPtr.Zero || !IsOpen)
        {
            Log.Error($"Cannot initialize '{Title}': Window handle is invalid or window is closed.");
            return false;
        }

        return Initialize();
    }

    private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Win32Window? window = null;
        IntPtr userDataPtr = IntPtr.Zero;

        if (msg == NativeMethods.WM_NCCREATE)
        {
            try
            {
                var cs = Marshal.PtrToStructure<NativeMethods.CREATESTRUCT>(lParam);
                var handle = GCHandle.FromIntPtr(cs.lpCreateParams);
                window = handle.Target as Win32Window;
                if (window != null)
                {
                    userDataPtr = cs.lpCreateParams; // Store for logging below
                    NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, userDataPtr);
                    Log.Info($"WM_NCCREATE: Associated GCHandle {userDataPtr} with HWND {hWnd} ('{window.Title}')");
                }
                else
                {
                    Log.Warning($"WM_NCCREATE: Failed to get window instance from GCHandle {cs.lpCreateParams} for HWND {hWnd}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error during WM_NCCREATE processing for HWND {hWnd}: {ex}");
            }
        }
        else
        {
            userDataPtr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
            if (userDataPtr != IntPtr.Zero)
            {
                try
                {
                    var handle = GCHandle.FromIntPtr(userDataPtr);
                    if (handle.IsAllocated && handle.Target != null)
                    {
                        window = handle.Target as Win32Window;
                    }
                    else if (handle.IsAllocated && handle.Target == null)
                    {
                        Log.Warning($"WindowProcedure: GCHandle {userDataPtr} for HWND {hWnd} target is null (likely finalized prematurely). Msg={msg:X}. Resetting GWLP_USERDATA.");
                        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
                    }
                    else
                    {
                        Log.Warning($"WindowProcedure: GCHandle {userDataPtr} for HWND {hWnd} is not allocated. Msg={msg:X}. Resetting GWLP_USERDATA.");
                        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
                    }
                }
                catch (InvalidOperationException)
                {
                    Log.Warning($"WindowProcedure: Invalid GCHandle {userDataPtr} retrieved for HWND {hWnd}, Msg={msg:X}. Resetting GWLP_USERDATA.");
                    NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error retrieving window instance from GCHandle {userDataPtr} for HWND {hWnd}: {ex}");
                }
            }
        }

        // ---> ADDED LOGGING <---
        if (window is ModalSecondaryWindow) // Check if the retrieved instance is a modal
        {
            // Log every message for the modal window HWND after NCCREATE potentially associated it
            Log.Info($"MODAL_WNDPROC_DEBUG: HWND={hWnd}, Msg={msg:X}, UserDataPtr={userDataPtr}, FoundInstance={window != null}, Title='{window?.Title ?? "N/A"}'");
        }
        // ---> END ADDED LOGGING <---

        if (window != null)
        {
            try
            {
                return window.HandleMessage(hWnd, msg, wParam, lParam);
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling message {msg:X} for HWND {hWnd} ('{window.Title}'): {ex}");
            }
        }
        else if (msg != NativeMethods.WM_NCCREATE && msg != NativeMethods.WM_NCDESTROY && msg != 24 /*WM_GETMINMAXINFO*/) // Suppress warning for expected early messages
        {
            Log.Warning($"WindowProcedure: No associated window instance found for HWND {hWnd} (UserData: {userDataPtr}) for message {msg:X}. Using DefWindowProc.");
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }


    protected virtual IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        int xPos = NativeMethods.GET_X_LPARAM(lParam);
        int yPos = NativeMethods.GET_Y_LPARAM(lParam);

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
                OnMouseMove(xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONDOWN:
                OnMouseDown(MouseButton.Left, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONUP:
                OnMouseUp(MouseButton.Left, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_RBUTTONDOWN:
                OnMouseDown(MouseButton.Right, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_RBUTTONUP:
                OnMouseUp(MouseButton.Right, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_MBUTTONDOWN:
                OnMouseDown(MouseButton.Middle, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_MBUTTONUP:
                OnMouseUp(MouseButton.Middle, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_XBUTTONDOWN:
                int xButton1 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                OnMouseDown(xButton1 == NativeMethods.XBUTTON1 ? MouseButton.XButton1 : MouseButton.XButton2, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_XBUTTONUP:
                int xButton2 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                OnMouseUp(xButton2 == NativeMethods.XBUTTON1 ? MouseButton.XButton1 : MouseButton.XButton2, xPos, yPos);
                return IntPtr.Zero;

            case NativeMethods.WM_MOUSEWHEEL:
                short wheelDelta = NativeMethods.GET_WHEEL_DELTA_WPARAM(wParam);
                OnMouseWheel(wheelDelta);
                return IntPtr.Zero;

            case NativeMethods.WM_KEYDOWN:
            case NativeMethods.WM_SYSKEYDOWN:
                int vkCodeDown = (int)wParam;
                OnKeyDown(vkCodeDown);

                if (vkCodeDown == NativeMethods.VK_ESCAPE && !IsKeyDownHandled(vkCodeDown))
                {
                    Close();
                }
                return IntPtr.Zero;

            case NativeMethods.WM_KEYUP:
            case NativeMethods.WM_SYSKEYUP:
                int vkCodeUp = (int)wParam;
                OnKeyUp(vkCodeUp);
                return IntPtr.Zero;

            case NativeMethods.WM_CLOSE:
                if (OnClose())
                {
                    NativeMethods.DestroyWindow(hWnd);
                }
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                Log.Info($"WM_DESTROY for {hWnd} ('{Title}').");
                OnDestroy();

                if (this is MainAppWindow)
                {
                    Log.Info("Main window destroyed, posting quit message.");
                    NativeMethods.PostQuitMessage(0);
                }
                else if (this is SecondaryWindow secWin)
                {
                    ApplicationCore.Instance.UnregisterSecondaryWindow(secWin);
                }
                return IntPtr.Zero;

            case NativeMethods.WM_NCDESTROY:
                Log.Info($"WM_NCDESTROY: Releasing GCHandle for {hWnd} ('{Title}').");
                IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
                if (ptr != IntPtr.Zero)
                {
                    try
                    {
                        var handle = GCHandle.FromIntPtr(ptr);
                        if (handle.IsAllocated)
                        {
                            Log.Info($"Freeing GCHandle {ptr} in WM_NCDESTROY for '{Title}' ({hWnd}).");
                            handle.Free();
                        }
                        else
                        {
                            Log.Warning($"WM_NCDESTROY: GCHandle {ptr} for '{Title}' ({hWnd}) was already freed.");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        Log.Warning($"WM_NCDESTROY: GCHandle {ptr} for '{Title}' ({hWnd}) was invalid upon retrieval.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error freeing GCHandle {ptr} on NCDESTROY for '{Title}' ({hWnd}): {ex.Message}");
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
            Log.Info($"Programmatically closing window {_hwnd} ('{Title}').");
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
                Log.Info($"Disposing Win32Window '{Title}' (managed)...");
                Cleanup();
            }

            Log.Info($"Disposing Win32Window '{Title}' (unmanaged)...");
            if (_hwnd != IntPtr.Zero)
            {
                Log.Info($"Requesting destroy for window {_hwnd} ('{Title}') during Dispose...");

                NativeMethods.DestroyWindow(_hwnd);
            }
            else
            {
                if (_gcHandle.IsAllocated)
                {
                    Log.Warning($"Freeing potentially dangling GCHandle for '{Title}' during Dispose (window handle was already zero)...");
                    try { _gcHandle.Free(); } catch (Exception ex) { Log.Error($"Error freeing GCHandle: {ex.Message}"); }
                }
            }

            _isDisposed = true;
            IsOpen = false;
            Log.Info($"Win32Window '{Title}' disposed.");
        }
    }

    ~Win32Window()
    {
        Log.Warning($"Win32Window Finalizer called for '{Title}'!");
        Dispose(false);
    }
}

public enum MouseButton { Left, Right, Middle, XButton1, XButton2 }