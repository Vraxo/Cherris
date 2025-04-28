/*
namespace Cherris;

public class ShaderCache
{
    public static ShaderCache Instance { get; } = new();

    private readonly Dictionary<string, Shader?> shaders = [];

    private ShaderCache() { }

    public Shader? Get(string key)
    {
        if (shaders.TryGetValue(key, out Shader? shader))
        {
            return shader;
        }

        var newSound = Shad.Load(key);

        if (newSound is null)
        {
            Log.Error($"[SoundCache] Could not load sound: {key}");
        }

        shaders[key] = newSound;
        return newSound;
    }
}
*/