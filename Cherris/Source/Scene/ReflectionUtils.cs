using System;
using System.Linq;
using System.Reflection;

namespace Cherris;

public static class ReflectionUtils
{
    private const BindingFlags MemberBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static MemberInfo GetMemberInfo(Type type, string memberName)
    {
        MemberInfo[] allMembers = type.GetMember(memberName, MemberBindingFlags | BindingFlags.IgnoreCase);

        if (allMembers.Length == 0)
        {
            throw new InvalidOperationException($"Member '{memberName}' not found on type '{type.Name}'");
        }

        if (allMembers.Length > 1)
        {
            var property = allMembers.OfType<PropertyInfo>().FirstOrDefault();
            if (property is not null)
            {
                return property;
            }

            var field = allMembers.OfType<FieldInfo>().FirstOrDefault();
            if (field != null)
            {
                return field;
            }

            Log.Error($"[GetMemberInfo] Ambiguity detected for member '{memberName}' on type '{type.Name}'. Found {allMembers.Length} members:");
            foreach (MemberInfo m in allMembers)
            {
                string memberTypeName = m switch { PropertyInfo p => p.PropertyType.Name, FieldInfo f => f.FieldType.Name, _ => m.MemberType.ToString() };
                Log.Error($"  - Name: {m.Name}, Kind: {m.MemberType}, Type: {memberTypeName}, Declared by: {m.DeclaringType?.FullName}");
            }

            throw new AmbiguousMatchException($"Ambiguous match found for member '{memberName}' on type '{type.Name}'.");
        }

        return allMembers[0];
    }

    public static Type GetMemberType(MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'")
        };
    }

    public static object? GetMemberValue(object target, MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            PropertyInfo p => p.GetValue(target),
            FieldInfo f => f.GetValue(target),
            _ => throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'")
        };
    }

    public static void SetMemberValue(object target, MemberInfo memberInfo, object value)
    {
        switch (memberInfo)
        {
            case PropertyInfo p:
                try
                {
                    p.SetValue(target, value);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed setting property '{p.Name}' on '{target.GetType().Name}': {ex.Message}");
                    throw;
                }
                break;

            case FieldInfo f:
                try
                {
                    f.SetValue(target, value);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed setting field '{f.Name}' on '{target.GetType().Name}': {ex.Message}");
                    throw;
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'");
        }
    }

    public static object CreateMemberInstance(MemberInfo memberInfo)
    {
        Type typeToCreate = memberInfo switch
        {
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            _ => throw new InvalidOperationException($"Unsupported member type '{memberInfo?.GetType().Name}' for member '{memberInfo?.Name}'")
        };

        return Activator.CreateInstance(typeToCreate) ?? throw new InvalidOperationException($"Failed to create instance of {typeToCreate.Name}");
    }
}