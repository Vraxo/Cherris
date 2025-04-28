using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cherris;

public class Animation
{
    public List<Keyframe> Keyframes { get; set; } = new();

    public class Keyframe
    {
        [YamlMember(Alias = "T")]
        public float Time { get; set; }

        public Dictionary<string, Dictionary<string, float>> Nodes { get; set; } = [];
    }

    public Animation() { }

    public Animation(string filePath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        Keyframes = deserializer.Deserialize<List<Keyframe>>(File.ReadAllText(filePath));
    }
}