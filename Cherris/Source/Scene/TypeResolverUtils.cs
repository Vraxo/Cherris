using System;
using System.Reflection;

namespace Cherris;

public static class TypeResolverUtils
{
    public static Type ResolveType(string typeName)
    {
        Type? foundType = null;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foundType = assembly.GetType(typeName, false, true);

            if (foundType != null)
            {
                break;
            }

            if (typeName.Contains('.'))
            {
                continue;
            }

            foundType = assembly.GetType($"Cherris.{typeName}", false, true);

            if (foundType != null)
            {
                break;
            }
        }

        return foundType ?? throw new TypeLoadException($"Type '{typeName}' not found in any loaded assembly.");
    }
}