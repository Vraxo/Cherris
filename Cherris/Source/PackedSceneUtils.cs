using System.Collections;
using System.Reflection;

namespace Cherris;

public static class PackedSceneUtils
{
    private const BindingFlags MemberBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly string[] SpecialProperties = { "type", "name", "path" };

    public static void SetProperties(Node node, Dictionary<string, object> element,
        List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        foreach (var (key, value) in element)
        {
            if (SpecialProperties.Contains(key)) continue;

            if (key == "CollisionLayers" && value is IList layerList)
            {
                //// Convert string collision layers to their corresponding integer values
                //var convertedLayers = layerList.Cast<object>().Select(layer =>
                //{
                //    if (layer is string layerName && CollisionServer.Instance.CollisionLayers.ContainsKey(layerName))
                //    {
                //        return CollisionServer.Instance.CollisionLayers[layerName];
                //    }
                //
                //    return Convert.ToInt32(layer); // for numeric layers
                //}).ToList();
                //
                //// Set the converted layers
                //SetNestedMember(node, key, convertedLayers, deferredNodeAssignments);
            }
            else
            {
                SetNestedMember(node, key, value, deferredNodeAssignments);
            }
        }
    }


    public static void SetNestedMember(object target, string memberPath, object value,
        List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        var pathParts = memberPath.Split('/');
        object currentObject = target;

        for (var i = 0; i < pathParts.Length; i++)
        {
            var memberInfo = GetMemberInfo(currentObject.GetType(), pathParts[i]);
            var isFinalSegment = i == pathParts.Length - 1;

            if (isFinalSegment)
            {
                HandleFinalSegment(target, memberPath, currentObject, memberInfo, value, deferredNodeAssignments);
            }
            else
            {
                currentObject = GetOrCreateIntermediateObject(currentObject, memberInfo);
            }
        }
    }

    private static MemberInfo GetMemberInfo(Type type, string memberName)
    {
        var property = type.GetProperty(memberName, MemberBindingFlags);
        if (property != null) return property;

        var field = type.GetField(memberName, MemberBindingFlags);
        if (field != null) return field;

        throw new InvalidOperationException($"Member '{memberName}' not found on type '{type.Name}'");
    }

    private static void HandleFinalSegment(object rootTarget, string memberPath, object currentObject,
        MemberInfo memberInfo, object value, List<(Node, string, object)>? deferredAssignments)
    {
        var memberType = memberInfo switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => throw new InvalidOperationException("Unsupported member type")
        };

        if (ShouldDeferAssignment(memberType, value))
        {
            deferredAssignments?.Add(((Node)rootTarget, memberPath, value));
        }
        else
        {
            var convertedValue = ConvertValue(memberType, value);
            SetMemberValue(currentObject, memberInfo, convertedValue);
        }
    }

    private static bool ShouldDeferAssignment(Type memberType, object value)
        => memberType.IsSubclassOf(typeof(Node)) && value is string;

    private static object GetOrCreateIntermediateObject(object currentObject, MemberInfo memberInfo)
    {
        var existingValue = GetMemberValue(currentObject, memberInfo);
        if (existingValue != null) return existingValue;

        var newInstance = CreateMemberInstance(memberInfo);
        SetMemberValue(currentObject, memberInfo, newInstance);
        return newInstance;
    }

    private static object CreateMemberInstance(MemberInfo memberInfo)
        => Activator.CreateInstance(memberInfo switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => throw new InvalidOperationException("Unsupported member type")
        }) ?? throw new InvalidOperationException("Failed to create instance");

    private static object? GetMemberValue(object target, MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            PropertyInfo p => p.GetValue(target),
            FieldInfo f => f.GetValue(target),
            _ => throw new InvalidOperationException("Unsupported member type")
        };
    }

    private static void SetMemberValue(object target, MemberInfo memberInfo, object value)
    {
        switch (memberInfo)
        {
            case PropertyInfo p:
                p.SetValue(target, value);
                break;
            case FieldInfo f:
                f.SetValue(target, value);
                break;
            default:
                throw new InvalidOperationException("Unsupported member type");
        }
    }

    public static Type ResolveType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, false) ?? assembly.GetType($"{assembly.GetName().Name}.{typeName}", false))
            .FirstOrDefault(type => type != null)
            ?? throw new TypeLoadException($"Type '{typeName}' not found");
    }

    public static object ConvertValue(Type targetType, object value)
    {
        return value switch
        {
            Dictionary<object, object> dict => ConvertNestedObject(targetType, dict),
            IList list => ConvertList(targetType, list),
            _ => ConvertPrimitive(targetType, value)
        };
    }

    private static object ConvertNestedObject(Type targetType, Dictionary<object, object> dict)
    {
        var instance = Activator.CreateInstance(targetType)
            ?? throw new InvalidOperationException($"Failed to create {targetType.Name} instance");

        foreach (var (key, value) in dict)
        {
            string memberName = key.ToString() ?? throw new InvalidDataException("Dictionary key cannot be null");
            MemberInfo memberInfo = GetMemberInfo(targetType, memberName);

            var convertedValue = ConvertValue(GetMemberType(memberInfo), value);
            SetMemberValue(instance, memberInfo, convertedValue);
        }

        return instance;
    }

    private static Type GetMemberType(MemberInfo memberInfo)
        => memberInfo switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => throw new InvalidOperationException("Unsupported member type")
        };

    private static object ConvertList(Type targetType, IList list)
    {
        if (targetType == typeof(List<int>))
            return list.Cast<object>().Select(Convert.ToInt32).ToList();

        return targetType.Name switch
        {
            nameof(Vector2) => ParseVector2(list),
            nameof(Color) => ParseColor(list),
            _ => throw new NotSupportedException($"Unsupported list conversion to {targetType}")
        };
    }

    private static object ConvertPrimitive(Type targetType, object value)
    {
        string stringValue = value.ToString()?.TrimQuotes()
            ?? throw new InvalidOperationException("Value cannot be null");

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, stringValue);
        }

        return targetType switch
        {
            _ when targetType == typeof(int)         => int.Parse(stringValue),
            _ when targetType == typeof(uint)        => uint.Parse(stringValue),
            _ when targetType == typeof(float)       => float.Parse(stringValue),
            _ when targetType == typeof(double)      => double.Parse(stringValue),
            _ when targetType == typeof(bool)        => bool.Parse(stringValue),
            _ when targetType == typeof(string)      => stringValue,
            //_ when targetType == typeof(AudioStream) => AudioStream.Load(stringValue)!,
            _ when targetType == typeof(AudioStream) => ResourceLoader.Load<AudioStream>(stringValue)!,
            _ when targetType == typeof(Sound)       => ResourceLoader.Load<Sound>(stringValue),
            _ when targetType == typeof(Animation)   => ResourceLoader.Load<Animation>(stringValue),
            _ when targetType == typeof(Texture)     => ResourceLoader.Load<Texture>(stringValue),
            _ when targetType == typeof(Font)        => ResourceLoader.Load<Font>(stringValue),
            _ => throw new NotSupportedException($"Unsupported type: {targetType.Name}")
        };
    }

    private static Vector2 ParseVector2(IList list)
    {
        if (list.Count != 2) 
        { 
            throw new ArgumentException("Vector2 requires exactly 2 elements");
        }

        return new(
            Convert.ToSingle(list[0]),
            Convert.ToSingle(list[1])
        );
    }

    private static Color ParseColor(IList list)
    {
        if (list.Count < 3 || list.Count > 4)
        {
            throw new ArgumentException("Color requires 3 or 4 elements");
        }

        return new(
            Convert.ToByte(list[0]),
            Convert.ToByte(list[1]),
            Convert.ToByte(list[2]),
            list.Count > 3 ? Convert.ToByte(list[3]) : (byte)255
        );
    }

    private static string TrimQuotes(this string input)
        => input.Length >= 2 && (input[0] == '"' || input[0] == '\'')
            ? input[1..^1]
            : input;
}