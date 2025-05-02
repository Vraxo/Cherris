using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vortice.Mathematics; // Ensure Color is resolved correctly

namespace Cherris;

public static class PackedSceneUtils
{
    private const BindingFlags MemberBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly string[] SpecialProperties = { "type", "name", "path", "children", "Node" }; // Added reserved keys

    public static void SetProperties(Node node, Dictionary<string, object> element,
        List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        foreach (var (key, value) in element)
        {
            if (SpecialProperties.Contains(key, StringComparer.OrdinalIgnoreCase)) continue; // Case-insensitive check

            SetNestedMember(node, key, value, deferredNodeAssignments);
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
        var allMembers = type.GetMember(memberName, MemberBindingFlags | BindingFlags.IgnoreCase); // Use IgnoreCase

        if (allMembers.Length == 0)
        {
            throw new InvalidOperationException($"Member '{memberName}' not found on type '{type.Name}'");
        }

        if (allMembers.Length > 1)
        {
            // Prefer Property over Field if ambiguous by case
            var property = allMembers.OfType<PropertyInfo>().FirstOrDefault();
            if (property != null) return property;
            var field = allMembers.OfType<FieldInfo>().FirstOrDefault();
            if (field != null) return field;

            Log.Error($"[GetMemberInfo] Ambiguity detected for member '{memberName}' on type '{type.Name}'. Found {allMembers.Length} members:");
            foreach (var m in allMembers)
            {
                string memberTypeName = m switch { PropertyInfo p => p.PropertyType.Name, FieldInfo f => f.FieldType.Name, _ => m.MemberType.ToString() };
                Log.Error($"  - Name: {m.Name}, Kind: {m.MemberType}, Type: {memberTypeName}, Declared by: {m.DeclaringType?.FullName}");
            }
            throw new AmbiguousMatchException($"Ambiguous match found for member '{memberName}' on type '{type.Name}'.");
        }

        return allMembers[0];
    }

    private static void HandleFinalSegment(object rootTarget, string memberPath, object currentObject,
        MemberInfo memberInfo, object value, List<(Node, string, object)>? deferredAssignments)
    {
        var memberType = memberInfo switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'")
        };

        if (ShouldDeferAssignment(memberType, value))
        {
            if (rootTarget is Node nodeTarget)
            {
                deferredAssignments?.Add((nodeTarget, memberPath, value));
            }
            else
            {
                Log.Error($"Cannot defer assignment for non-Node root target type: {rootTarget.GetType().Name}");
            }
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
            _ => throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'")
        }) ?? throw new InvalidOperationException("Failed to create instance");

    private static object? GetMemberValue(object target, MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            PropertyInfo p => p.GetValue(target),
            FieldInfo f => f.GetValue(target),
            _ => throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'")
        };
    }

    private static void SetMemberValue(object target, MemberInfo memberInfo, object value)
    {
        switch (memberInfo)
        {
            case PropertyInfo p:
                try { p.SetValue(target, value); } catch (Exception ex) { Log.Error($"Failed setting property '{p.Name}' on '{target.GetType().Name}': {ex.Message}"); throw; }
                break;
            case FieldInfo f:
                try { f.SetValue(target, value); } catch (Exception ex) { Log.Error($"Failed setting field '{f.Name}' on '{target.GetType().Name}': {ex.Message}"); throw; }
                break;
            default:
                throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'");
        }
    }

    public static Type ResolveType(string typeName)
    {
        Type? foundType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foundType = assembly.GetType(typeName, false, true); // Case-insensitive
            if (foundType != null) break;

            if (!typeName.Contains('.'))
            {
                foundType = assembly.GetType($"Cherris.{typeName}", false, true); // Case-insensitive
                if (foundType != null) break;
            }
        }

        return foundType ?? throw new TypeLoadException($"Type '{typeName}' not found in any loaded assembly.");
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
            _ => throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'")
        };

    private static object ConvertList(Type targetType, IList list)
    {
        if (targetType == typeof(List<int>))
            return list.Cast<object>().Select(Convert.ToInt32).ToList();
        if (targetType == typeof(Vector2)) return ParseVector2(list);
        if (targetType == typeof(Color)) return ParseColor(list);

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var itemType = targetType.GetGenericArguments()[0];
            var genericList = (IList)Activator.CreateInstance(targetType)!;
            foreach (var item in list)
            {
                genericList.Add(Convert.ChangeType(item, itemType, CultureInfo.InvariantCulture));
            }
            return genericList;
        }

        throw new NotSupportedException($"Unsupported list conversion to type {targetType}");
    }


    private static object ConvertPrimitive(Type targetType, object value)
    {
        string stringValue = value?.ToString()?.TrimQuotes() ?? "";

        if (targetType.IsEnum)
        {
            try
            {
                // Special handling for SystemBackdropType
                if (targetType == typeof(SystemBackdropType))
                {
                    return Enum.Parse<SystemBackdropType>(stringValue, true);
                }

                return Enum.Parse(targetType, stringValue, true);
            }
            catch (Exception ex) { throw new InvalidOperationException($"Failed to parse enum '{targetType.Name}' from value '{stringValue}'.", ex); }
        }

        TypeCode typeCode = Type.GetTypeCode(targetType);
        try
        {
            switch (typeCode)
            {
                case TypeCode.Int32: return int.Parse(stringValue, CultureInfo.InvariantCulture);
                case TypeCode.UInt32: return uint.Parse(stringValue, CultureInfo.InvariantCulture);
                case TypeCode.Single: return float.Parse(stringValue, CultureInfo.InvariantCulture);
                case TypeCode.Double: return double.Parse(stringValue, CultureInfo.InvariantCulture);
                case TypeCode.Boolean: return bool.Parse(stringValue);
                case TypeCode.String: return stringValue;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert primitive value '{stringValue}' to type {targetType.Name}.", ex);
        }

        // Resource loading remains the same
        if (targetType == typeof(AudioStream)) return ResourceLoader.Load<AudioStream>(stringValue)!;
        if (targetType == typeof(Sound)) return ResourceLoader.Load<Sound>(stringValue)!;
        if (targetType == typeof(Animation)) return ResourceLoader.Load<Animation>(stringValue)!;
        if (targetType == typeof(Texture)) return ResourceLoader.Load<Texture>(stringValue)!;
        if (targetType == typeof(Font)) return ResourceLoader.Load<Font>(stringValue)!;

        try
        {
            return Convert.ChangeType(stringValue, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new NotSupportedException($"Unsupported primitive/resource type conversion from '{value?.GetType().Name ?? "null"}' to '{targetType.Name}' for value '{stringValue}'.", ex);
        }
    }

    private static Vector2 ParseVector2(IList list)
    {
        if (list.Count != 2) throw new ArgumentException($"Vector2 requires exactly 2 elements, got {list.Count}");
        try
        {
            return new(Convert.ToSingle(list[0], CultureInfo.InvariantCulture), Convert.ToSingle(list[1], CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { throw new ArgumentException($"Failed to parse Vector2 elements: {ex.Message}", ex); }
    }

    private static Color ParseColor(IList list)
    {
        if (list.Count < 3 || list.Count > 4) throw new ArgumentException($"Color4 requires 3 or 4 elements (R, G, B, [A]), got {list.Count}");
        try
        {
            float r = ConvertToFloatColor(list[0]);
            float g = ConvertToFloatColor(list[1]);
            float b = ConvertToFloatColor(list[2]);
            float a = list.Count > 3 ? ConvertToFloatColor(list[3]) : 1.0f;
            return new Color(r, g, b, a);
        }
        catch (Exception ex) { throw new ArgumentException($"Failed to parse Color4 elements: {ex.Message}", ex); }
    }

    private static float ConvertToFloatColor(object component)
    {
        float value = Convert.ToSingle(component, CultureInfo.InvariantCulture);
        if (value > 1.0f && value <= 255.0f) return value / 255.0f;
        return Math.Clamp(value, 0.0f, 1.0f);
    }

    private static string TrimQuotes(this string input)
        => input != null && input.Length >= 2 && ((input[0] == '"' && input[^1] == '"') || (input[0] == '\'' && input[^1] == '\''))
            ? input[1..^1]
            : input ?? "";
}