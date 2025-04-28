namespace Cherris;

public class AudioStreamCache
{
    public static AudioStreamCache? Instance => field ??= new();

    private readonly Dictionary<string, AudioStream?> audioStreams = [];

    private AudioStreamCache() { }

    public AudioStream? Get(string filePath)
    {
        if (audioStreams.TryGetValue(filePath, out AudioStream? audio))
        {
            return audio;
        }

        AudioStream? newAudio = AudioStream.Load(filePath);

        if (newAudio is null)
        {
            Log.Error($"Could not load audio stream: {filePath}");
            return null;
        }

        audioStreams[filePath] = newAudio;
        return newAudio;
    }
}