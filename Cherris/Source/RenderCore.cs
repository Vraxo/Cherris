using Raylib_cs;

namespace Cherris;

public sealed class RenderCore
{
    private static RenderCore? _instance;
    public static RenderCore Instance => _instance ??= new();

    public Camera? Camera;
    public Shader? PostProcessingShader { get; set; }

    private readonly List<DrawCommand> drawCommands = [];
    private RenderTexture2D renderTexture;

    // Main

    private RenderCore()
    {
        Vector2 windowSize = DisplayServer.Instance.OriginalWindowSize;
        renderTexture = Raylib.LoadRenderTexture((int)windowSize.X, (int)windowSize.Y);

        DisplayServer.Instance.WindowSizeChanged += WindowSizeChanged;

        //PostProcessingShader = Shader.Load(null, "Res/Shaders/Bloom.fs");
    }

    private void WindowSizeChanged(Vector2 obj)
    {
        renderTexture = Raylib.LoadRenderTexture(
            (int)DisplayServer.WindowSize.X,
            (int)DisplayServer.WindowSize.Y);
    }

    public void Process()
    {
        Raylib.BeginTextureMode(renderTexture);
            //Raylib.ClearBackground(Color.DarkGray);
            BeginCameraMode();
                ProcessDrawCommands();
            EndCameraMode();
        Raylib.EndTextureMode();
    
        BeginShaderMode(PostProcessingShader);
            //Rectangle source = new(0, 0, renderTexture.Texture.Width, -renderTexture.Texture.Height);
            //Raylib.DrawTextureRec(renderTexture.Texture, source, Vector2.Zero, Color.White);
        EndShaderMode();
    }

    public void Process2()
    {
        //Raylib.ClearBackground(Color.DarkGray);
        //BeginShaderMode(PostProcessingShader);
        //    BeginCameraMode();
        //        ProcessDrawCommands();
        //    EndCameraMode();
        //EndShaderMode();
    }

    public void Submit(Action drawAction, int layer)
    {
        drawCommands.Add(new(drawAction, layer));
    }

    public Vector2 GetScreenToWorld(Vector2 position)
    {
        return Camera is null
            ? position
            : Raylib.GetScreenToWorld2D(position, Camera);
    }

    public Vector2 GetWorldToScreen(Vector2 position)
    {
        return Camera is null
            ? position 
            : Raylib.GetWorldToScreen2D(position, Camera);
    }

    // Scissor mode

    public static void BeginScissorMode(Vector2 position, Vector2 size)
    {
        Raylib.BeginScissorMode(
            (int)position.X,
            (int)position.Y,
            (int)size.X,
            (int)size.Y);
    }

    public static void EndScissorMode()
    {
        Raylib.EndScissorMode();
    }

    // Camera

    public void SetCamera(Camera camera)
    {
        Camera = camera;
    }

    private void BeginCameraMode()
    {
        if (Camera is null)
        {
            return;
        }

        Camera2D cam = new()
        {
            Target = Camera.GlobalPosition,
            Offset = DisplayServer.WindowSize / 2,
            Zoom = Camera.Zoom,
        };

        Raylib.BeginMode2D(cam);
    }

    private void EndCameraMode()
    {
        if (Camera is null)
        {
            return;
        }

        Raylib.EndMode2D();
    }

    // Shader mode

    public static void BeginShaderMode(Shader? shader)
    {
        if (shader is null)
        {
            return;
        }

        Raylib.BeginShaderMode(shader);
    }

    public static void EndShaderMode()
    {
        Raylib.EndShaderMode();
    }

    // Other

    private void ProcessDrawCommands()
    {
        foreach (DrawCommand command in drawCommands.OrderBy(c => c.Layer))
        {
            command.DrawAction.Invoke();
        }

        drawCommands.Clear();
    }

    private class DrawCommand(Action drawAction, int layer)
    {
        public Action DrawAction { get; } = drawAction;
        public int Layer { get; } = layer;
    }
}