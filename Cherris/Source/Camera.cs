using Raylib_cs;

namespace Cherris;

public class Camera : Node2D
{
    public float Zoom { get; set; } = 1;

    public void SetAsActive()
    {
        RenderCore.Instance.SetCamera(this);
    }

    public static implicit operator Camera2D(Camera camera)
    {
        return new()
        {
            Target = camera.GlobalPosition,
            Offset = DisplayServer.WindowSize / 2,
            Zoom = camera.Zoom,
        };
    }
}