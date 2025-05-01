using System;
using System.Numerics;
using System.Diagnostics;
using System.Collections.Generic;

using Vortice;
using Vortice.Mathematics;

using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using SharpGen.Runtime;

using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using SizeI = Vortice.Mathematics.SizeI;
using Rect = Vortice.Mathematics.Rect;

using Vortice.DCommon;

namespace Cherris;

public abstract class Direct2DAppWindow : Win32Window
{
    protected ID2D1Factory1? d2dFactory;
    protected IDWriteFactory? dwriteFactory;
    protected ID2D1HwndRenderTarget? renderTarget;

    // Ensure background is black for best Mica compatibility
    protected Color4 backgroundColor = Colors.Black;
    protected bool graphicsInitialized = false;

    private Stopwatch fpsTimer = new();
    private long lastFpsUpdateTimeTicks = 0;
    private int frameCountSinceUpdate = 0;
    private float currentFps = 0.0f;
    private const long FpsUpdateIntervalTicks = TimeSpan.TicksPerSecond / 2;
    private ID2D1SolidColorBrush? fpsTextBrush;
    private IDWriteTextFormat? fpsTextFormat;
    private readonly Color4 fpsTextColor = DefaultTheme.Text;
    private readonly string fpsFontName = "Consolas";
    private readonly float fpsFontSize = 14.0f;

    private Dictionary<Color4, ID2D1SolidColorBrush> brushCache = new();
    private Dictionary<string, IDWriteTextFormat> textFormatCache = new();

    public Direct2DAppWindow(string title = "Vortice DirectUI Base Window", int width = 800, int height = 600)
        : base(title, width, height)
    { }

    protected override bool Initialize()
    {
        Log.Info($"Direct2DAppWindow '{Title}' initializing Vortice Graphics...");
        return InitializeGraphics();
    }

    protected override void Cleanup()
    {
        Log.Info($"Direct2DAppWindow '{Title}' cleaning up its resources...");
        CleanupGraphics();
    }

    public override void RenderFrame()
    {
        if (!fpsTimer.IsRunning)
        {
            fpsTimer.Start();
            lastFpsUpdateTimeTicks = fpsTimer.ElapsedTicks;
            frameCountSinceUpdate = 0;
        }

        frameCountSinceUpdate++;
        long elapsedTicks = fpsTimer.ElapsedTicks;
        long timeSinceLastUpdate = elapsedTicks - lastFpsUpdateTimeTicks;

        if (timeSinceLastUpdate >= FpsUpdateIntervalTicks)
        {
            float secondsElapsed = (float)timeSinceLastUpdate / TimeSpan.TicksPerSecond;
            currentFps = (secondsElapsed > 0.001f) ? (frameCountSinceUpdate / secondsElapsed) : 0.0f;
            frameCountSinceUpdate = 0;
            lastFpsUpdateTimeTicks = elapsedTicks;
        }

        if (!graphicsInitialized || renderTarget is null || dwriteFactory is null)
        {
            if (!graphicsInitialized && Handle != nint.Zero && IsOpen)
            {
                Log.Warning($"Graphics not initialized in RenderFrame for '{Title}', attempting reinitialization.");
                InitializeGraphics();
                if (!graphicsInitialized || renderTarget is null || dwriteFactory is null)
                {
                    Log.Error($"Reinitialization failed in RenderFrame for '{Title}'.");
                    return;
                }
            }
            else
            {
                Log.Warning($"RenderFrame skipped for '{Title}': Graphics not ready or window closed.");
                return;
            }
        }

        try
        {
            renderTarget.BeginDraw();
            // Clear with the designated background color (should be black for Mica)
            renderTarget.Clear(backgroundColor);

            var drawingContext = new DrawingContext(renderTarget, dwriteFactory, this);

            DrawUIContent(drawingContext);

            if (fpsTextBrush is not null && fpsTextFormat is not null)
            {
                string fpsText = $"FPS: {currentFps:F1}";
                Rect fpsLayoutRect = new Rect(5f, 5f, 150f, 30f);
                renderTarget.DrawText(fpsText, fpsTextFormat, fpsLayoutRect, fpsTextBrush);
            }

            Result endDrawResult = renderTarget.EndDraw();

            if (endDrawResult.Failure)
            {
                Log.Error($"EndDraw failed for '{Title}': {endDrawResult.Description}");
                if (endDrawResult.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Log.Warning($"Render target needs recreation for '{Title}' (Detected in EndDraw).");
                    graphicsInitialized = false;
                    CleanupGraphics();
                    InitializeGraphics();
                }
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Log.Error($"Render target needs recreation for '{Title}' (Caught SharpGenException in RenderFrame): {ex.Message}");
            graphicsInitialized = false;
            CleanupGraphics();
            InitializeGraphics();
        }
        catch (Exception ex)
        {
            Log.Error($"Rendering Error in RenderFrame for '{Title}': {ex}");
            graphicsInitialized = false;
            CleanupGraphics();
            InitializeGraphics();
        }
    }

    protected abstract void DrawUIContent(DrawingContext context);

    protected override void OnSize(int width, int height)
    {
        if (graphicsInitialized && renderTarget is not null && width > 0 && height > 0)
        {
            Log.Info($"Window '{Title}' resized to {width}x{height}. Resizing render target...");
            try
            {
                var newPixelSize = new SizeI(width, height);

                CleanupDeviceSpecificResources();

                renderTarget.Resize(newPixelSize);

                RecreateDeviceSpecificResources();

                Log.Info($"Successfully resized render target for '{Title}'.");
                Invalidate();
            }
            catch (SharpGenException ex)
            {
                Log.Error($"Failed to resize Render Target for '{Title}' (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
                if (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Log.Warning($"Render target needs recreation for '{Title}' (Detected in Resize Exception).");
                    graphicsInitialized = false;
                    CleanupGraphics();
                    InitializeGraphics();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to resize Render Target for '{Title}' (General Exception): {ex}");
                graphicsInitialized = false;
                CleanupGraphics();
                InitializeGraphics();
            }
        }
        else if (!graphicsInitialized && Handle != nint.Zero && IsOpen && width > 0 && height > 0)
        {
            Log.Warning($"OnSize called for '{Title}' but graphics not initialized. Attempting initialization.");
            InitializeGraphics();
        }
        else if (width <= 0 || height <= 0)
        {
            Log.Warning($"Ignoring OnSize call for '{Title}' with invalid dimensions: {width}x{height}");
        }
    }

    protected override void OnMouseMove(int x, int y) { }
    protected override void OnMouseDown(MouseButton button, int x, int y) { }
    protected override void OnMouseUp(MouseButton button, int x, int y) { }
    protected override void OnKeyDown(int keyCode) { }
    protected override void OnKeyUp(int keyCode) { }
    protected override void OnMouseWheel(short delta) { }

    protected virtual bool InitializeGraphics()
    {
        if (graphicsInitialized) return true;
        if (Handle == nint.Zero || !IsOpen)
        {
            Log.Warning($"InitializeGraphics skipped for '{Title}': Invalid handle or window not open.");
            return false;
        }

        Log.Info($"Attempting Graphics Initialization for '{Title}' HWND {Handle}...");

        try
        {
            CleanupGraphics();

            Result factoryResult = D2D1.D2D1CreateFactory(D2DFactoryType.SingleThreaded, out d2dFactory);
            factoryResult.CheckError();
            if (d2dFactory is null) throw new InvalidOperationException($"D2D Factory creation failed silently for '{Title}'.");

            Result dwriteResult = DWrite.DWriteCreateFactory(DW.FactoryType.Shared, out dwriteFactory);
            dwriteResult.CheckError();
            if (dwriteFactory is null) throw new InvalidOperationException($"DWrite Factory creation failed silently for '{Title}'.");

            var clientRectSize = GetClientRectSize();
            if (clientRectSize.Width <= 0 || clientRectSize.Height <= 0)
            {
                Log.Warning($"Invalid client rect size ({clientRectSize.Width}x{clientRectSize.Height}) for '{Title}'. Aborting graphics initialization.");
                CleanupGraphics();
                return false;
            }

            // Use Premultiplied alpha, which is generally compatible with DWM effects.
            var dxgiPixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var renderTargetProperties = new RenderTargetProperties(dxgiPixelFormat);
            var hwndRenderTargetProperties = new HwndRenderTargetProperties
            {
                Hwnd = Handle,
                PixelSize = new SizeI(clientRectSize.Width, clientRectSize.Height),
                PresentOptions = PresentOptions.None
            };

            renderTarget = d2dFactory.CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties);
            if (renderTarget is null) throw new InvalidOperationException($"Render target creation returned null unexpectedly for '{Title}'.");

            renderTarget.TextAntialiasMode = D2D.TextAntialiasMode.Cleartype;

            brushCache = new Dictionary<Color4, ID2D1SolidColorBrush>();
            textFormatCache = new Dictionary<string, IDWriteTextFormat>();

            RecreateDeviceSpecificResources();

            frameCountSinceUpdate = 0;
            currentFps = 0;
            lastFpsUpdateTimeTicks = 0;
            fpsTimer.Restart();

            Log.Info($"Vortice Graphics initialized successfully for '{Title}' HWND {Handle}.");
            graphicsInitialized = true;
            return true;
        }
        catch (SharpGenException ex)
        {
            Log.Error($"Graphics Initialization failed for '{Title}' (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
            CleanupGraphics(); graphicsInitialized = false; return false;
        }
        catch (Exception ex)
        {
            Log.Error($"Graphics Initialization failed for '{Title}' (General Exception): {ex}");
            CleanupGraphics(); graphicsInitialized = false; return false;
        }
    }

    private void RecreateDeviceSpecificResources()
    {
        if (renderTarget is null || dwriteFactory is null) return;

        try
        {
            fpsTextFormat?.Dispose();
            fpsTextFormat = dwriteFactory.CreateTextFormat(fpsFontName, null, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, fpsFontSize, "en-us");
            fpsTextFormat.TextAlignment = DW.TextAlignment.Leading;
            fpsTextFormat.ParagraphAlignment = ParagraphAlignment.Near;

            fpsTextBrush?.Dispose();
            fpsTextBrush = renderTarget.CreateSolidColorBrush(fpsTextColor);

            Log.Info($"Recreated FPS drawing resources for '{Title}'.");
        }
        catch (Exception ex)
        {
            Log.Error($"Warning: Failed to recreate device-specific resources for '{Title}': {ex.Message}");
            CleanupDeviceSpecificResources();
        }
    }

    private void CleanupDeviceSpecificResources()
    {
        fpsTextBrush?.Dispose(); fpsTextBrush = null;
        fpsTextFormat?.Dispose(); fpsTextFormat = null;

        foreach (var brush in brushCache.Values) brush?.Dispose();
        brushCache.Clear();
        foreach (var format in textFormatCache.Values) format?.Dispose();
        textFormatCache.Clear();
        Log.Info($"Cleaned device-specific resources (brushes, formats) for '{Title}'.");
    }

    protected virtual void CleanupGraphics()
    {
        bool resourcesExisted = d2dFactory is not null || renderTarget is not null || dwriteFactory is not null;
        if (resourcesExisted) Log.Info($"Cleaning up Vortice Graphics resources for '{Title}'...");

        fpsTimer.Stop();

        CleanupDeviceSpecificResources();

        renderTarget?.Dispose(); renderTarget = null;
        dwriteFactory?.Dispose(); dwriteFactory = null;
        d2dFactory?.Dispose(); d2dFactory = null;
        graphicsInitialized = false;

        if (resourcesExisted) Log.Info($"Finished cleaning graphics resources for '{Title}'.");
    }

    protected SizeI GetClientRectSize()
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }

        int baseWidth = Math.Max(1, Width);
        int baseHeight = Math.Max(1, Height);
        if (Handle != nint.Zero)
        {
            Log.Warning($"GetClientRect failed for '{Title}'. Falling back to stored size: {baseWidth}x{baseHeight}");
        }
        return new SizeI(baseWidth, baseHeight);
    }

    public ID2D1SolidColorBrush? GetOrCreateBrush(Color4 color)
    {
        if (renderTarget is null)
        {
            Log.Warning($"GetOrCreateBrush called on '{Title}' with null RenderTarget.");
            return null;
        }

        if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush) && brush is not null)
        {
            return brush;
        }
        else if (brushCache.ContainsKey(color))
        {
            brushCache.Remove(color);
        }

        try
        {
            brush = renderTarget.CreateSolidColorBrush(color);
            if (brush is not null)
            {
                brushCache[color] = brush;
            }
            return brush;
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Log.Warning($"Recreate target detected in GetOrCreateBrush for color {color} on '{Title}'.");
            CleanupDeviceSpecificResources();
            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error creating brush for color {color} on '{Title}': {ex.Message}");
            return null;
        }
    }

    public IDWriteTextFormat? GetOrCreateTextFormat(ButtonStyle style)
    {
        if (dwriteFactory is null || style is null)
        {
            Log.Warning($"GetOrCreateTextFormat called on '{Title}' with null DWriteFactory or null style.");
            return null;
        }

        string cacheKey = $"{style.FontName}_{style.FontSize}_{style.FontWeight}_{style.FontStyle}_{style.FontStretch}";

        if (textFormatCache.TryGetValue(cacheKey, out IDWriteTextFormat? format) && format is not null)
        {
            return format;
        }
        else if (textFormatCache.ContainsKey(cacheKey))
        {
            textFormatCache.Remove(cacheKey);
        }

        try
        {
            format = dwriteFactory.CreateTextFormat(
                style.FontName,
                null,
                style.FontWeight,
                style.FontStyle,
                style.FontStretch,
                style.FontSize,
                "en-us"
            );

            if (format is not null)
            {
                textFormatCache[cacheKey] = format;
            }
            return format;
        }
        catch (Exception ex)
        {
            Log.Error($"Error creating text format for key {cacheKey} on '{Title}': {ex.Message}");
            return null;
        }
    }
}