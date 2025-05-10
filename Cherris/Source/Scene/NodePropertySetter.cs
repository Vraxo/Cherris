namespace Cherris;

public static class NodePropertySetter
{
    private static readonly string[] SpecialProperties = { "type", "name", "path", "children", "Node" };

    public static void SetProperties(Node node, Dictionary<string, object> element, List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        foreach ((string key, object value) in element)
        {
            if (SpecialProperties.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            SetNestedMember(node, key, value, deferredNodeAssignments);
        }
    }

    public static void SetNestedMember(object target, string memberPath, object value, List<(Node, string, object)>? deferredNodeAssignments = null)
    {
        string[] pathParts = memberPath.Split('/');
        object currentObject = target;

        for (var i = 0; i < pathParts.Length; i++)
        {
            var memberInfo = ReflectionUtils.GetMemberInfo(currentObject.GetType(), pathParts[i]);
            bool isFinalSegment = i == pathParts.Length - 1;

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

    private static void HandleFinalSegment(object rootTarget, string memberPath, object currentObject, System.Reflection.MemberInfo memberInfo, object value, List<(Node, string, object)>? deferredAssignments)
    {
        var memberType = ReflectionUtils.GetMemberType(memberInfo);

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
            var convertedValue = ValueConversionUtils.ConvertValue(memberType, value);
            ReflectionUtils.SetMemberValue(currentObject, memberInfo, convertedValue);
        }
    }

    private static bool ShouldDeferAssignment(Type memberType, object value)
    {
        return memberType.IsSubclassOf(typeof(Node)) && value is string;
    }

    private static object GetOrCreateIntermediateObject(object currentObject, System.Reflection.MemberInfo memberInfo)
    {
        object? existingValue = ReflectionUtils.GetMemberValue(currentObject, memberInfo);

        if (existingValue != null)
        {
            return existingValue;
        }

        object newInstance = ReflectionUtils.CreateMemberInstance(memberInfo);

        ReflectionUtils.SetMemberValue(currentObject, memberInfo, newInstance);

        return newInstance;
    }
}