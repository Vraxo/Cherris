using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

namespace Cherris;

public abstract class VisualItem : Node
{
    private bool fieldVisible = true;
    private int fieldLayer = 0;


    public bool Visible
    {
        get => fieldVisible;
        set
        {
            if (fieldVisible == value)
            {
                return;
            }

            fieldVisible = value;
            VisibleChanged?.Invoke(this, fieldVisible);
        }
    }

    public int Layer
    {
        get => fieldLayer;
        set
        {
            if (fieldLayer == value)
            {
                return;
            }

            fieldLayer = value;
            LayerChanged?.Invoke(this, fieldLayer);
        }
    }

    public delegate void VisibleEvent(VisualItem sender, bool visible);
    public delegate void LayerEvent(VisualItem sender, int layer);

    public event VisibleEvent? VisibleChanged;
    public event LayerEvent? LayerChanged;

    public virtual void Draw(DrawingContext context) { }


    protected void DrawStyledRectangle(DrawingContext context, Rect bounds, BoxStyle style)
    {
        if (context.OwnerWindow is null || context.RenderTarget is null || style is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        try
        {
            DrawBoxStyleHelper(context, bounds, style);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Log.Warning("Recreate target detected in DrawStyledRectangle.");


        }
        catch (Exception ex)
        {
            Log.Error($"Error drawing styled rectangle: {ex.Message}");
        }
    }

    protected void DrawFormattedText(DrawingContext context, string text, Rect layoutRect, ButtonStyle style, HAlignment hAlignment, VAlignment vAlignment)
    {
        if (string.IsNullOrEmpty(text) || context.OwnerWindow is null || context.RenderTarget is null || style is null || layoutRect.Width <= 0 || layoutRect.Height <= 0)
        {
            return;
        }

        ID2D1SolidColorBrush? textBrush = context.OwnerWindow.GetOrCreateBrush(style.FontColor);
        IDWriteTextFormat? textFormat = context.OwnerWindow.GetOrCreateTextFormat(style);

        if (textBrush is null || textFormat is null)
        {

            return;
        }

        try
        {
            textFormat.TextAlignment = hAlignment switch
            {
                HAlignment.Left => DW.TextAlignment.Leading,
                HAlignment.Center => DW.TextAlignment.Center,
                HAlignment.Right => DW.TextAlignment.Trailing,
                _ => DW.TextAlignment.Leading
            };
            textFormat.ParagraphAlignment = vAlignment switch
            {
                VAlignment.Top => DW.ParagraphAlignment.Near,
                VAlignment.Center => DW.ParagraphAlignment.Center,
                VAlignment.Bottom => DW.ParagraphAlignment.Far,
                _ => DW.ParagraphAlignment.Near
            };

            context.RenderTarget.DrawText(
                text,
                textFormat,
                layoutRect,
                textBrush,
                D2D.DrawTextOptions.Clip
            );
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Log.Warning("Recreate target detected in DrawFormattedText.");


        }
        catch (Exception ex)
        {
            Log.Error($"Error drawing formatted text '{text}': {ex.Message}");
        }
    }


    private static void DrawBoxStyleHelper(DrawingContext context, Rect bounds, BoxStyle style)
    {
        var renderTarget = context.RenderTarget;
        var ownerWindow = context.OwnerWindow;
        if (renderTarget is null || ownerWindow is null || style is null || bounds.Width <= 0 || bounds.Height <= 0) return;

        ID2D1SolidColorBrush? fillBrush = ownerWindow.GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush? borderBrush = ownerWindow.GetOrCreateBrush(style.BorderColor);

        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null && (borderTop > 0 || borderRight > 0 || borderBottom > 0 || borderLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        if (style.Roundness > 0.0f)
        {
            float maxRadius = Math.Min(bounds.Width * 0.5f, bounds.Height * 0.5f);
            float radius = Math.Max(0f, maxRadius * float.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(radius) && radius >= 0)
            {
                if (hasVisibleBorder && borderBrush is not null)
                {
                    System.Drawing.RectangleF outerRectF = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    RoundedRectangle outerRoundedRect = new(outerRectF, radius, radius);
                    renderTarget.FillRoundedRectangle(outerRoundedRect, borderBrush);
                }

                if (hasVisibleFill && fillBrush is not null)
                {
                    float fillX = bounds.X + borderLeft;
                    float fillY = bounds.Y + borderTop;
                    float fillWidth = Math.Max(0f, bounds.Width - borderLeft - borderRight);
                    float fillHeight = Math.Max(0f, bounds.Height - borderTop - borderBottom);

                    if (fillWidth > 0 && fillHeight > 0)
                    {
                        float avgBorderX = (borderLeft + borderRight) * 0.5f;
                        float avgBorderY = (borderTop + borderBottom) * 0.5f;
                        float innerRadiusX = Math.Max(0f, radius - avgBorderX);
                        float innerRadiusY = Math.Max(0f, radius - avgBorderY);

                        System.Drawing.RectangleF fillRectF = new(fillX, fillY, fillWidth, fillHeight);
                        RoundedRectangle fillRoundedRect = new(fillRectF, innerRadiusX, innerRadiusY);
                        renderTarget.FillRoundedRectangle(fillRoundedRect, fillBrush);
                    }
                    else if (!hasVisibleBorder)
                    {

                        System.Drawing.RectangleF outerRectF = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                        RoundedRectangle outerRoundedRect = new(outerRectF, radius, radius);
                        renderTarget.FillRoundedRectangle(outerRoundedRect, fillBrush);
                    }
                }
                return;
            }
        }


        if (hasVisibleBorder && borderBrush is not null)
        {
            renderTarget.FillRectangle(bounds, borderBrush);
        }

        if (hasVisibleFill && fillBrush is not null)
        {
            float fillX = bounds.X + borderLeft;
            float fillY = bounds.Y + borderTop;
            float fillWidth = Math.Max(0f, bounds.Width - borderLeft - borderRight);
            float fillHeight = Math.Max(0f, bounds.Height - borderTop - borderBottom);

            if (fillWidth > 0 && fillHeight > 0)
            {
                renderTarget.FillRectangle(new Rect(fillX, fillY, fillWidth, fillHeight), fillBrush);
            }
            else if (!hasVisibleBorder)
            {

                renderTarget.FillRectangle(bounds, fillBrush);
            }
        }
    }
}