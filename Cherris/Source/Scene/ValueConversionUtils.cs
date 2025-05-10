using System.Collections;
using System.Globalization;

namespace Cherris;

public static class ValueConversionUtils
{
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
        object instance = Activator.CreateInstance(targetType)
            ?? throw new InvalidOperationException($"Failed to create {targetType.Name} instance");

        foreach (var (key, val) in dict)
        {
            string memberName = key.ToString() ?? throw new InvalidDataException("Dictionary key cannot be null");
            var memberInfo = ReflectionUtils.GetMemberInfo(targetType, memberName);
            var memberActualType = ReflectionUtils.GetMemberType(memberInfo);
            var convertedValue = ConvertValue(memberActualType, val);
            ReflectionUtils.SetMemberValue(instance, memberInfo, convertedValue);
        }

        return instance;
    }

    private static object ConvertList(Type targetType, IList list)
    {
        if (targetType == typeof(List<int>))
        {
            return list.Cast<object>().Select(Convert.ToInt32).ToList();
        }

        if (targetType == typeof(Vector2))
        {
            return ParseVector2(list);
        }

        if (targetType == typeof(Color))
        {
            return ParseColor(list);
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            Type itemType = targetType.GetGenericArguments()[0];
            var genericList = (IList)Activator.CreateInstance(targetType)!;

            foreach (object? item in list)
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
                // if (targetType == typeof(SystemBackdropType)) // Assuming SystemBackdropType is defined elsewhere
                // {
                //     return Enum.Parse<SystemBackdropType>(stringValue, true);
                // }
                return Enum.Parse(targetType, stringValue, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse enum '{targetType.Name}' from value '{stringValue}'.", ex);
            }
        }

        TypeCode typeCode = Type.GetTypeCode(targetType);

        try
        {
            switch (typeCode)
            {
                case TypeCode.Int32:
                    return int.Parse(stringValue, CultureInfo.InvariantCulture);

                case TypeCode.UInt32:
                    return uint.Parse(stringValue, CultureInfo.InvariantCulture);

                case TypeCode.Single:
                    return float.Parse(stringValue, CultureInfo.InvariantCulture);

                case TypeCode.Double:
                    return double.Parse(stringValue, CultureInfo.InvariantCulture);

                case TypeCode.Boolean:
                    return bool.Parse(stringValue);

                case TypeCode.String:
                    return stringValue;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert primitive value '{stringValue}' to type {targetType.Name}.", ex);
        }

        if (targetType == typeof(AudioStream))
        {
            return ResourceLoader.Load<AudioStream>(stringValue)!;
        }

        if (targetType == typeof(Sound))
        {
            return ResourceLoader.Load<Sound>(stringValue)!;
        }

        if (targetType == typeof(Animation))
        {
            return ResourceLoader.Load<Animation>(stringValue)!;
        }

        if (targetType == typeof(Texture))
        {
            return ResourceLoader.Load<Texture>(stringValue)!;
        }

        if (targetType == typeof(Font))
        {
            return ResourceLoader.Load<Font>(stringValue)!;
        }

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
        if (list.Count != 2)
        {
            throw new ArgumentException($"Vector2 requires exactly 2 elements, got {list.Count}");
        }

        try
        {
            var x = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
            var y = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
            return new Vector2(x, y);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse Vector2 elements: {ex.Message}", ex);
        }
    }

    private static Color ParseColor(IList list)
    {
        if (list.Count < 3 || list.Count > 4)
        {
            throw new ArgumentException($"Color requires 3 or 4 elements (R, G, B, [A]), got {list.Count}");
        }

        try
        {
            float r = ConvertToFloatColor(list[0]);
            float g = ConvertToFloatColor(list[1]);
            float b = ConvertToFloatColor(list[2]);
            float a = list.Count > 3 ? ConvertToFloatColor(list[3]) : 1.0f;

            return new Color(r, g, b, a);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse Color elements: {ex.Message}", ex);
        }
    }

    private static float ConvertToFloatColor(object component)
    {
        float value = Convert.ToSingle(component, CultureInfo.InvariantCulture);

        return value > 1.0f && value <= 255.0f
            ? value / 255.0f
            : float.Clamp(value, 0.0f, 1.0f);
    }
}