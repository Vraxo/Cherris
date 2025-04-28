namespace Cherris;

public class AnimationCache
{
    private static AnimationCache? _instance;
    public static AnimationCache Instance => _instance ??= new AnimationCache();

    private readonly Dictionary<string, Animation> animations = [];

    private AnimationCache() { }

    public Animation Get(string animationPath)
    {
        if (animations.TryGetValue(animationPath, out Animation? animation))
        {
            return animation;
        }

        Animation newAnimation = new(animationPath);
        return newAnimation;
    }

    public void Dispose()
    {
        animations.Clear();
    }
}