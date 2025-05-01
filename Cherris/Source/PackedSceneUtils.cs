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
            var memberInfo = GetMemberInfo(currentObject.GetType(), pathParts[i]); // Use the potentially ambiguous MemberInfo
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

    // MODIFIED: Added logging to detect ambiguity source.
    private static MemberInfo GetMemberInfo(Type type, string memberName)
    {
        // Find all potentially matching members first for diagnostics
        var allMembers = type.GetMember(memberName, MemberBindingFlags);

        if (allMembers.Length == 0)
        {
            throw new InvalidOperationException($"Member '{memberName}' not found on type '{type.Name}'");
        }

        if (allMembers.Length > 1)
        {
            // Log detailed information about the ambiguous members found
            Log.Error($"[GetMemberInfo] Ambiguity detected for member '{memberName}' on type '{type.Name}'. Found {allMembers.Length} members:");
            foreach (var m in allMembers)
            {
                string memberTypeName = m switch { PropertyInfo p => p.PropertyType.Name, FieldInfo f => f.FieldType.Name, _ => m.MemberType.ToString() };
                Log.Error($"  - Name: {m.Name}, Kind: {m.MemberType}, Type: {memberTypeName}, Declared by: {m.DeclaringType?.FullName}");
            }

            // Even if GetMember finds multiple, GetProperty/GetField might resolve it based on C# rules.
            // Let's try them first.
            var property = type.GetProperty(memberName, MemberBindingFlags);
            if (property != null)
            {
                Log.Info($"[GetMemberInfo] Ambiguity resolved by GetProperty for '{memberName}' on '{type.Name}'. Using property declared by {property.DeclaringType?.FullName}.");
                return property;
            }

            var field = type.GetField(memberName, MemberBindingFlags);
            if (field != null)
            {
                Log.Info($"[GetMemberInfo] Ambiguity resolved by GetField for '{memberName}' on '{type.Name}'. Using field declared by {field.DeclaringType?.FullName}.");
                return field;
            }

            // If GetProperty/GetField couldn't resolve, the ambiguity is real for reflection's typical use.
            // Throw the standard exception. The logs above provide context.
            Log.Error($"[GetMemberInfo] Could not resolve ambiguity for '{memberName}' using GetProperty/GetField despite GetMember finding multiple matches.");
            throw new AmbiguousMatchException($"Ambiguous match found for member '{memberName}' on type '{type.Name}'. See previous log messages for details.");
        }

        // Exactly one member found by GetMember
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
        // Adjusted to handle potential namespace differences
        Type? foundType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Try direct lookup first
            foundType = assembly.GetType(typeName, false);
            if (foundType != null) break;

            // Try prepending common namespace if not fully qualified
            if (!typeName.Contains('.'))
            {
                foundType = assembly.GetType($"Cherris.{typeName}", false);
                if (foundType != null) break;
            }
            // Add other potential namespaces if needed
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
            MemberInfo memberInfo = GetMemberInfo(targetType, memberName); // Use modified GetMemberInfo

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

        // Use typeof comparisons for clarity and robustness
        if (targetType == typeof(Vector2)) return ParseVector2(list);
        if (targetType == typeof(Color)) return ParseColor(list); // Assuming Color is Vortice.Mathematics.Color4

        // Fallback or error for other list types
        // This might need expansion if you serialize other list types like List<string>, etc.
        try
        {
            // Attempt generic list conversion if applicable (e.g., List<float>)
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = targetType.GetGenericArguments()[0];
                var genericList = (IList)Activator.CreateInstance(targetType)!;
                foreach (var item in list)
                {
                    genericList.Add(Convert.ChangeType(item, itemType));
                }
                return genericList;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error during generic list conversion for type {targetType}: {ex.Message}");
        }


        throw new NotSupportedException($"Unsupported list conversion to type {targetType}");
    }


    private static object ConvertPrimitive(Type targetType, object value)
    {
        string stringValue = value?.ToString()?.TrimQuotes()
            ?? ""; // Handle null value case

        if (string.IsNullOrEmpty(stringValue) && targetType != typeof(string) && Nullable.GetUnderlyingType(targetType) == null)
        {
            // Handle empty strings trying to be parsed as non-string, non-nullable types if necessary
            // For now, let downstream parsing handle errors, but could add specific checks here.
            // Example: return default(T) if T is targetType, but that's complex.
        }

        if (targetType.IsEnum)
        {
            try { return Enum.Parse(targetType, stringValue, true); } // Use case-insensitive parsing
            catch (Exception ex) { throw new InvalidOperationException($"Failed to parse enum '{targetType.Name}' from value '{stringValue}'.", ex); }
        }

        // Use TypeCode for efficient primitive conversions where possible
        TypeCode typeCode = Type.GetTypeCode(targetType);
        try
        {
            switch (typeCode)
            {
                case TypeCode.Int32: return int.Parse(stringValue);
                case TypeCode.UInt32: return uint.Parse(stringValue);
                case TypeCode.Single: return float.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture); // Use InvariantCulture for floats
                case TypeCode.Double: return double.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture);
                case TypeCode.Boolean: return bool.Parse(stringValue);
                case TypeCode.String: return stringValue; // Already a string
                                                          // Add other TypeCodes if needed (Int16, Byte, Decimal, DateTime, etc.)
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert primitive value '{stringValue}' to type {targetType.Name}.", ex);
        }

        // Handle non-primitive types resolved by ResourceLoader or specific classes
        if (targetType == typeof(AudioStream)) return ResourceLoader.Load<AudioStream>(stringValue)!;
        if (targetType == typeof(Sound)) return ResourceLoader.Load<Sound>(stringValue)!;
        if (targetType == typeof(Animation)) return ResourceLoader.Load<Animation>(stringValue)!;
        if (targetType == typeof(Texture)) return ResourceLoader.Load<Texture>(stringValue)!;
        if (targetType == typeof(Font)) return ResourceLoader.Load<Font>(stringValue)!;
        // Consider PackedScene type for nested scenes?
        // if (targetType == typeof(PackedScene)) return new PackedScene(stringValue);

        // If no specific handler, attempt ChangeType as a last resort
        try
        {
            return Convert.ChangeType(stringValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new NotSupportedException($"Unsupported primitive/resource type conversion from '{value?.GetType().Name ?? "null"}' to '{targetType.Name}' for value '{stringValue}'.", ex);
        }
    }

    private static Vector2 ParseVector2(IList list)
    {
        if (list.Count != 2)
        {
            throw new ArgumentException($"Vector2 requires exactly 2 elements, got {list.Count}");
        }

        try
        {
            return new(
                Convert.ToSingle(list[0], System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToSingle(list[1], System.Globalization.CultureInfo.InvariantCulture)
            );
        }
        catch (Exception ex) { throw new ArgumentException($"Failed to parse Vector2 elements: {ex.Message}", ex); }
    }

    // Updated to match Vortice.Mathematics.Color4 (float components 0-1)
    private static Color ParseColor(IList list)
    {
        if (list.Count < 3 || list.Count > 4)
        {
            throw new ArgumentException($"Color4 requires 3 or 4 elements (R, G, B, [A]), got {list.Count}");
        }

        try
        {
            // Assume values are 0-255 bytes if integers, or 0-1 floats if floating point
            // Convert to float 0-1 range needed by Color4
            float r = ConvertToFloatColor(list[0]);
            float g = ConvertToFloatColor(list[1]);
            float b = ConvertToFloatColor(list[2]);
            float a = list.Count > 3 ? ConvertToFloatColor(list[3]) : 1.0f; // Default alpha to 1.0f

            return new Color(r, g, b, a);
        }
        catch (Exception ex) { throw new ArgumentException($"Failed to parse Color4 elements: {ex.Message}", ex); }
    }

    // Helper to convert object (likely int/byte or float/double) to float 0-1
    private static float ConvertToFloatColor(object component)
    {
        float value = Convert.ToSingle(component, System.Globalization.CultureInfo.InvariantCulture);
        // If value seems to be in 0-255 range, normalize it. Otherwise, assume it's already 0-1.
        // This is heuristic - might need adjustment based on typical YAML values.
        if (value > 1.0f && value <= 255.0f)
        {
            return value / 255.0f;
        }
        // Clamp to valid range
        return Math.Clamp(value, 0.0f, 1.0f);
    }


    private static string TrimQuotes(this string input)
        => input != null && input.Length >= 2 && ((input[0] == '"' && input[^1] == '"') || (input[0] == '\'' && input[^1] == '\''))
            ? input[1..^1]
            : input ?? ""; // Return empty if input is null
}