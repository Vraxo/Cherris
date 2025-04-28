using Cherris;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class FileLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .Build();

    public static T Load<T>(string filePath) where T : new()
    {
        var yamlContent = File.ReadAllText(filePath);
        var data = _deserializer.Deserialize<object>(yamlContent);

        T instance = new();
        ProcessYamlData(instance, data, "");
        return instance;
    }

    private static void ProcessYamlData(object target, object yamlData, string currentPath)
    {
        switch (yamlData)
        {
            case Dictionary<object, object> dict:
                foreach (var entry in dict)
                {
                    string key = entry.Key.ToString()!;
                    string newPath = string.IsNullOrEmpty(currentPath) ? key : $"{currentPath}/{key}";
                    ProcessYamlData(target, entry.Value, newPath);
                }
                break;
            case List<object> list:
                // Treat lists as values (e.g., for Color, Vector2)
                PackedSceneUtils.SetNestedMember(target, currentPath, list);
                break;
            default:
                PackedSceneUtils.SetNestedMember(target, currentPath, yamlData);
                break;
        }
    }
}