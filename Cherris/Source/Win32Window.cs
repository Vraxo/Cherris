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
            Log.Error($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
            if (_gcHandle.IsAllocated) _gcHandle.Free();
            return false;
        }

        Log.Info($"Window '{_windowTitle}' created with HWND: {_hwnd}");
        IsOpen = true;

        ApplySystemBackdrop();

        return true;
    }

    protected virtual NativeMethods.DWMSBT GetSystemBackdropType()
    {
        return NativeMethods.DWMSBT.DWMSBT_NONE;
    }

    protected void ApplySystemBackdrop()
    {
        if (_hwnd == IntPtr.Zero || !IsOpen) return;

        var backdropTypeEnum = GetSystemBackdropType();
        if (backdropTypeEnum == NativeMethods.DWMSBT.DWMSBT_NONE) return;

        var osVersion = Environment.OSVersion.Version;
        bool requiresWin1122H2 = backdropTypeEnum != NativeMethods.DWMSBT.DWMSBT_AUTO; // Basic Mica/Acrylic need 22000, specific types need 22621+
        int requiredBuild = 22621; // Build for Mica Alt, Tabbed, Acrylic

        if (osVersion.Major < 10 || (osVersion.Major == 10 && osVersion.Build < requiredBuild))
        {
            Log.Warning($"System backdrop type {backdropTypeEnum} requires Windows 11 Build {requiredBuild} or later. Current: {osVersion}");
            return;
        }

        try
        {
            // 1. Set Backdrop Type
            int backdropTypeValue = (int)backdropTypeEnum;
            int result = NativeMethods.DwmSetWindowAttribute(
                _hwnd,
                NativeMethods.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropTypeValue,
                sizeof(int));

            if (result != 0) // S_OK is 0
            {
                Log.Error($"DwmSetWindowAttribute failed for {NativeMethods.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE} with HRESULT: 0x{result:X8} on HWND {_hwnd} ('{_windowTitle}').");
                // Don't attempt dark mode if backdrop failed
                return;
            }
            Log.Info($"Applied system backdrop type {backdropTypeEnum} to HWND {_hwnd} ('{_windowTitle}').");

            // 2. Set Dark Mode (Force TRUE for testing)
            // Requires Windows 10 Build 19041+ for DWMWA_USE_IMMERSIVE_DARK_MODE
            // Requires Windows 10 Build 22621+ for this specific attribute value (20)
            int useDarkMode = 1; // 1 = True (Use Dark Mode), 0 = False (Use Light Mode)
            result = NativeMethods.DwmSetWindowAttribute(
                _hwnd,
                NativeMethods.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDarkMode,
                sizeof(int));

            if (result != 0)
            {
                // This might fail on slightly older Win11 builds, but backdrop might still work. Log as warning.
                Log.Warning($"DwmSetWindowAttribute failed for {NativeMethods.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE} with HRESULT: 0x{result:X8} on HWND {_hwnd} ('{_windowTitle}'). This might be expected on some builds.");
            }
            else
            {
                Log.Info($"Applied {NativeMethods.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE}=TRUE to HWND {_hwnd} ('{_windowTitle}').");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Exception applying system backdrop/theme attributes to HWND {_hwnd} ('{_windowTitle}'): {ex.Message}");
        }
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
                }
                catch (InvalidOperationException)
                {
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
                Log.Error($"Error handling message {msg} for HWND {hWnd} ('{window.Title}'): {ex}");
            }
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

            case NativeMethods.WM_DWMCOMPOSITIONCHANGED:
                Log.Info($"WM_DWMCOMPOSITIONCHANGED received for {hWnd} ('{Title}'). Reapplying backdrop.");
                ApplySystemBackdrop();
                break; // Fall through to DefWindowProc

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
                break; // Fall through to DefWindowProc
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
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
            Log.Info($"Win32Window '{Title}' dispose initiated.");
        }
    }

    ~Win32Window()
    {
        Log.Warning($"Win32Window Finalizer called for '{Title}'! Ensure Dispose() was called.");
        Dispose(disposing: false);
    }
}

public enum MouseButton { Left, Right, Middle, XButton1, XButton2 }