namespace Cherris;

public sealed class ResourceLoader
{
    public static T? Load<T>(string path)
    {
        return typeof(T) switch
        {
            var t when t == typeof(AudioStream) => (T)(object)AudioStreamCache.Instance.Get(path)!,
            var t when t == typeof(Texture) => (T)(object)TextureCache.Instance.Get(path),
            var t when t == typeof(Font) => (T)(object)FontCache.Instance.Get(path),
            var t when t == typeof(Sound) => (T)(object)SoundCache.Instance.Get(path),
            var t when t == typeof(Animation) => (T)(object)AnimationCache.Instance.Get(path),
            _ => throw new InvalidOperationException($"Unsupported resource type: {typeof(T)}")
        };
    }
}