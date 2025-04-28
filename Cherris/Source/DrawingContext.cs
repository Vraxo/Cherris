using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace Cherris;


public readonly struct DrawingContext
{
    public readonly ID2D1HwndRenderTarget RenderTarget;
    public readonly IDWriteFactory DWriteFactory;
    public readonly Direct2DAppWindow OwnerWindow;


    public DrawingContext(ID2D1HwndRenderTarget renderTarget, IDWriteFactory dwriteFactory, Direct2DAppWindow ownerWindow)
    {
        RenderTarget = renderTarget;
        DWriteFactory = dwriteFactory;
        OwnerWindow = ownerWindow;
    }
}