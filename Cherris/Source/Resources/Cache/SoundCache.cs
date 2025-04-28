namespace Cherris;

public class SoundCache
{
    public static SoundCache? Instance => field ??= new();

    private readonly Dictionary<string, Sound?> soundEffects = [];

    private SoundCache() { }

    public Sound? Get(string soundKey)
    {
        if (soundEffects.TryGetValue(soundKey, out Sound? soundEffect))
        {
            return soundEffect;
        }

        Sound? newSound = Sound.Load(soundKey);

        if (newSound is null)
        {
            Log.Error($"Could not load sound: {soundKey}");
        }

        soundEffects[soundKey] = newSound;
        return newSound;
    }
}